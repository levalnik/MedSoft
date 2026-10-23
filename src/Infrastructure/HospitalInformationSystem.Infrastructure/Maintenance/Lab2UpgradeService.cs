using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Persistence.Configurations;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalInformationSystem.Infrastructure.Maintenance;

public sealed class Lab2UpgradeService(HospitalDbContext db, TimeProvider timeProvider)
{
    public async Task<Lab2UpgradeReport> ReportAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await ReadReportAsync(connection, cancellationToken);
    }

    public async Task ApplyAsync(Lab2UpgradePlan plan, CancellationToken cancellationToken = default)
    {
        if (plan.Version != 1 || plan.UserNames is null || plan.MedicalWorkerRoles is null || plan.RetiredAdministrators is null)
            throw new ArgumentException("Неверная версия или структура плана перехода.");
        foreach (var role in plan.MedicalWorkerRoles.Values) _ = MedicalWorkerRoleStorage.Read(role);
        var renamedAccountIds = new HashSet<Guid>();

        await using (var connection = new NpgsqlConnection(db.Database.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            var report = await ReadReportAsync(connection, cancellationToken);
            ValidatePlan(plan, report);
            if (report.Accounts.Count > 0)
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await ExecuteAsync(connection, "LOCK TABLE \"AspNetUsers\", \"AspNetUserRoles\" IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
                report = await ReadReportAsync(connection, cancellationToken);
                ValidatePlan(plan, report);
                var renames = plan.UserNames.Where(pair => report.Accounts.Any(account => account.Id == pair.Key
                    && (account.UserName != pair.Value || account.RequiresRename))).ToArray();
                foreach (var (id, _) in renames)
                    await RenameAsync(connection, id, "_mig" + Guid.NewGuid().ToString("N")[..12], cancellationToken);
                foreach (var (id, name) in renames)
                {
                    await RenameAsync(connection, id, name, cancellationToken);
                    renamedAccountIds.Add(id);
                }
                foreach (var id in plan.RetiredAdministrators)
                {
                    await using var command = new NpgsqlCommand("""
                        DELETE FROM "AspNetUserRoles" ur USING "AspNetRoles" r
                        WHERE ur."RoleId" = r."Id" AND r."Name" = 'Administrator' AND ur."UserId" = @id
                        """, connection);
                    command.Parameters.AddWithValue("id", id);
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
            }
        }

        await db.Database.MigrateAsync(cancellationToken);
        await using var finalTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var id in renamedAccountIds)
        {
            var renamed = await db.Users.SingleAsync(x => x.Id == id, cancellationToken);
            renamed.UpdatedAtUtc = timeProvider.GetUtcNow();
        }
        foreach (var (id, role) in plan.MedicalWorkerRoles)
        {
            var worker = await db.MedicalWorkers.SingleAsync(x => x.Id == id, cancellationToken);
            var nextRole = MedicalWorkerRoleStorage.Read(role);
            if (worker.Role != nextRole)
            {
                worker.Role = nextRole;
                worker.UpdatedAtUtc = timeProvider.GetUtcNow();
            }
        }
        foreach (var id in plan.RetiredAdministrators)
        {
            var user = await db.Users.SingleAsync(x => x.Id == id, cancellationToken);
            if (user.IsActive || user.IsAdministrator)
            {
                user.IsActive = false;
                user.IsAdministrator = false;
                user.SecurityStamp = Guid.NewGuid().ToString();
            }
        }
        if (plan.RetireLegacyCredentials)
        {
            var users = await db.Users.ToListAsync(cancellationToken);
            var legacyUsers = users.Where(x => IsLegacyHash(x.PasswordHash)).ToArray();
            var legacyIds = legacyUsers.Select(x => x.Id).ToArray();
            var setup = await db.AdministratorSetupStates.SingleOrDefaultAsync(cancellationToken);
            if (setup is { IsCompleted: true } && legacyUsers.Any(user => user.IsAdministrator))
                throw new InvalidOperationException("Первоначальная настройка уже завершена; её нельзя сбрасывать этой операцией.");
            foreach (var user in legacyUsers)
            {
                user.PasswordHash = null;
                user.IsActive = false;
                user.SecurityStamp = Guid.NewGuid().ToString();
            }
            await db.UserTokens.Where(x => legacyIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            await db.RefreshSessions.Where(x => legacyIds.Contains(x.UserId) && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAtUtc, now)
                    .SetProperty(x => x.RevokeReason, "legacy-credential-retirement"), cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        await finalTransaction.CommitAsync(cancellationToken);
    }

    private static bool IsLegacyHash(string? hash) => hash is not null
        && !hash.StartsWith("$argon2", StringComparison.Ordinal)
        && !hash.StartsWith("$2", StringComparison.Ordinal)
        && !hash.StartsWith("$scrypt$", StringComparison.Ordinal);

    private static void ValidatePlan(Lab2UpgradePlan plan, Lab2UpgradeReport report)
    {
        if (plan.UserNames.Keys.Except(report.Accounts.Select(x => x.Id)).Any()
            || plan.MedicalWorkerRoles.Keys.Except(report.MedicalWorkerIds).Any())
            throw new ArgumentException("План содержит UUID несуществующего аккаунта или сотрудника.");
        var finalNames = report.Accounts.Select(x => plan.UserNames.GetValueOrDefault(x.Id, x.UserName ?? "")).ToArray();
        if (finalNames.Any(name => !AccountUserNameRules.IsValid(name))
            || report.Accounts.Any(account => account.RequiresRename && !plan.UserNames.ContainsKey(account.Id)))
            throw new ArgumentException("Для каждого несовместимого логина задайте новое имя: 3–16 символов A–Z, a–z, 0–9, . _ @ + -.");
        if (finalNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != finalNames.Length)
            throw new ArgumentException("Новые логины не уникальны без учёта регистра.");
        var administrators = report.Accounts.Where(x => x.Roles.Contains(ApplicationRoles.Administrator)).Select(x => x.Id).ToArray();
        if (plan.RetiredAdministrators.Distinct().Count() != plan.RetiredAdministrators.Length
            || plan.RetiredAdministrators.Any(id => !administrators.Contains(id) && !report.Accounts.Any(a => a.Id == id && a.Roles.Length == 0))
            || administrators.Except(plan.RetiredAdministrators).Count() > 1)
            throw new ArgumentException("Оставьте не более одного администратора; UUID остальных явно укажите в retiredAdministrators.");
    }

    private static async Task<Lab2UpgradeReport> ReadReportAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var accounts = new List<Lab2AccountUpgradeReport>();
        await using var exists = new NpgsqlCommand("SELECT to_regclass('\"AspNetUsers\"') IS NOT NULL", connection);
        if ((bool)(await exists.ExecuteScalarAsync(cancellationToken))!)
        {
            await using var command = new NpgsqlCommand("""
                SELECT u."Id", u."UserName", u."NormalizedUserName", u."PasswordHash",
                    ARRAY(SELECT r."Name" FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON ur."RoleId" = r."Id" WHERE ur."UserId" = u."Id")
                FROM "AspNetUsers" u ORDER BY u."Id"
                """, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.IsDBNull(1) ? null : reader.GetString(1);
                var normalized = reader.IsDBNull(2) ? null : reader.GetString(2);
                accounts.Add(new(reader.GetGuid(0), name, reader.GetFieldValue<string[]>(4),
                    name is null || !AccountUserNameRules.IsValid(name) || normalized != name.ToUpperInvariant(),
                    IsLegacyHash(reader.IsDBNull(3) ? null : reader.GetString(3))));
            }
        }
        var workers = new List<Guid>();
        await using var workersExist = new NpgsqlCommand("SELECT to_regclass('medical_workers') IS NOT NULL", connection);
        if ((bool)(await workersExist.ExecuteScalarAsync(cancellationToken))!)
        {
            await using var command = new NpgsqlCommand("SELECT id FROM medical_workers ORDER BY id", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) workers.Add(reader.GetGuid(0));
        }
        return new(accounts, workers, accounts.Count(x => x.Roles.Contains(ApplicationRoles.Administrator)) > 1);
    }

    private static async Task RenameAsync(NpgsqlConnection connection, Guid id, string name, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE "AspNetUsers" SET "UserName" = @name, "NormalizedUserName" = @normalized,
                "SecurityStamp" = @stamp, "ConcurrencyStamp" = @stamp WHERE "Id" = @id
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("normalized", name.ToUpperInvariant());
        command.Parameters.AddWithValue("stamp", Guid.NewGuid().ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
