using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Domain.MedicalWorkers;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Maintenance;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class Lab2UpgradeTests
{
    [Fact]
    public async Task Explicit_upgrade_preserves_medical_data_and_uuid_without_guessing_roles_or_resetting_passwords()
    {
        await using var legacy = await LegacyDatabase.CreateAsync();
        var db = legacy.Db;
        var account = await legacy.AddAccountAsync("legacy.administrator.long", administrator: true);
        var workerId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var historyId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var diagnosisId = Guid.NewGuid();
        var prescriptionId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO medical_workers (id, first_name, last_name, position, is_deleted, created_at_utc, updated_at_utc)
            VALUES ({workerId}, 'Алексей', 'Иванов', 'Врач', false, {now}, {now});
            INSERT INTO patients (id, first_name, last_name, birth_date, sex, created_at_utc, updated_at_utc)
            VALUES ({patientId}, 'Иван', 'Петров', DATE '1990-01-01', 1, {now}, {now});
            INSERT INTO medical_records (id, patient_id, created_at_utc, version)
            VALUES ({recordId}, {patientId}, {now}, 1);
            INSERT INTO diagnosis_definitions (id, code, name, created_at_utc, updated_at_utc)
            VALUES ({definitionId}, 'I10.00', 'Гипертензия', {now}, {now});
            INSERT INTO medical_record_diagnoses (id, medical_record_id, diagnosis_definition_id, diagnosis_code_snapshot,
                diagnosis_name_snapshot, added_at_utc, added_by_medical_worker_id)
            VALUES ({diagnosisId}, {recordId}, {definitionId}, 'I10.00', 'Гипертензия', {now}, {workerId});
            INSERT INTO prescriptions (id, medical_record_id, text, created_at_utc, created_by_medical_worker_id)
            VALUES ({prescriptionId}, {recordId}, 'Контроль давления', {now}, {workerId});
            INSERT INTO medical_record_history_events (id, medical_record_id, version, occurred_at_utc,
                performed_by_medical_worker_id, performed_by_name_snapshot, performed_by_position_snapshot, event_type, payload_json)
            VALUES ({historyId}, {recordId}, 1, {now}, {workerId}, 'Иванов Алексей', 'Врач', 'MedicalRecordCreated', jsonb_build_object());
            """);
        var service = new Lab2UpgradeService(db, TimeProvider.System);
        var report = await service.ReportAsync();
        report.Accounts.Single().Id.Should().Be(account);
        report.Accounts.Single().RequiresRename.Should().BeTrue();
        report.Accounts.Single().HasLegacyPassword.Should().BeTrue();
        var failedMigration = () => db.Database.MigrateAsync();
        (await failedMigration.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("Maintenance report");

        var plan = new Lab2UpgradePlan { UserNames = { [account] = "legacy.admin" } };
        await service.ApplyAsync(plan);
        var worker = await db.MedicalWorkers.SingleAsync(x => x.Id == workerId);
        worker.Role.Should().Be(MedicalWorkerRole.Other);
        worker.CreatedAtUtc.Should().Be(now);
        (await db.Patients.SingleAsync()).Id.Should().Be(patientId);
        (await db.MedicalRecords.SingleAsync()).Id.Should().Be(recordId);
        (await db.MedicalRecordHistoryEvents.SingleAsync()).Id.Should().Be(historyId);
        (await db.DiagnosisDefinitions.SingleAsync()).Id.Should().Be(definitionId);
        (await db.MedicalRecordDiagnoses.SingleAsync()).Id.Should().Be(diagnosisId);
        (await db.Prescriptions.SingleAsync()).Id.Should().Be(prescriptionId);
        var user = await db.Users.SingleAsync();
        user.Id.Should().Be(account);
        user.UserName.Should().Be("legacy.admin");
        user.PasswordHash.Should().Be("legacy-test-hash");
        user.IsActive.Should().BeTrue();
        user.IsAdministrator.Should().BeTrue();
        var stamp = user.SecurityStamp;
        db.ChangeTracker.Clear();
        await service.ApplyAsync(plan);
        (await db.Users.SingleAsync()).SecurityStamp.Should().Be(stamp);
        (await db.MedicalRecordHistoryEvents.CountAsync()).Should().Be(1);
        db.Database.HasPendingModelChanges().Should().BeFalse();

        db.ChangeTracker.Clear();
        await service.ApplyAsync(new Lab2UpgradePlan { MedicalWorkerRoles = { [workerId] = "doctor" } });
        (await db.MedicalWorkers.SingleAsync()).Role.Should().Be(MedicalWorkerRole.Doctor);
    }

    [Fact]
    public async Task Colliding_renames_are_rejected_before_any_account_changes()
    {
        await using var legacy = await LegacyDatabase.CreateAsync();
        var first = await legacy.AddAccountAsync("legacy.account.one");
        var second = await legacy.AddAccountAsync("legacy.account.two");
        var service = new Lab2UpgradeService(legacy.Db, TimeProvider.System);
        var plan = new Lab2UpgradePlan { UserNames = { [first] = "same.name", [second] = "SAME.NAME" } };
        var apply = () => service.ApplyAsync(plan);
        await apply.Should().ThrowAsync<ArgumentException>().WithMessage("*не уникальны*");
        var report = await service.ReportAsync();
        report.Accounts.Should().OnlyContain(x => x.UserName!.StartsWith("legacy.account."));
        (await legacy.Db.Database.GetPendingMigrationsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Multiple_old_administrators_require_explicit_retirement_and_cannot_reappear_on_repeated_upgrade()
    {
        await using var legacy = await LegacyDatabase.CreateAsync();
        var first = await legacy.AddAccountAsync("admin.one", administrator: true);
        var second = await legacy.AddAccountAsync("admin.two", administrator: true);
        var service = new Lab2UpgradeService(legacy.Db, TimeProvider.System);
        (await service.ReportAsync()).RequiresAdministratorSelection.Should().BeTrue();
        var invalid = () => service.ApplyAsync(new Lab2UpgradePlan());
        await invalid.Should().ThrowAsync<ArgumentException>().WithMessage("*администратора*");
        var plan = new Lab2UpgradePlan { RetiredAdministrators = [second] };
        await service.ApplyAsync(plan);
        legacy.Db.ChangeTracker.Clear();
        await service.ApplyAsync(plan);
        var admin = await legacy.Db.Users.SingleAsync(x => x.IsAdministrator);
        admin.Id.Should().Be(first);
        var retired = await legacy.Db.Users.SingleAsync(x => x.Id == second);
        retired.IsActive.Should().BeFalse();
        retired.PasswordHash.Should().Be("legacy-test-hash");
        (await legacy.Db.UserRoles.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Credential_retirement_is_explicit_repeatable_and_does_not_reset_completed_setup()
    {
        await using var legacy = await LegacyDatabase.CreateAsync();
        var admin = await legacy.AddAccountAsync("admin.one", administrator: true);
        var service = new Lab2UpgradeService(legacy.Db, TimeProvider.System);
        await service.ApplyAsync(new Lab2UpgradePlan());
        var user = await legacy.Db.Users.SingleAsync();
        user.PasswordHash.Should().NotBeNull();
        var now = DateTimeOffset.UtcNow;
        var setup = new AdministratorSetupStateDbo
        {
            Id = 1, AdministratorUserId = admin, IsCompleted = true, TotpSecretProtected = "synthetic-protected-value",
            CreatedAtUtc = now, UpdatedAtUtc = now, CompletedAtUtc = now
        };
        legacy.Db.AdministratorSetupStates.Add(setup);
        await legacy.Db.SaveChangesAsync();
        var denied = () => service.ApplyAsync(new Lab2UpgradePlan { RetireLegacyCredentials = true });
        await denied.Should().ThrowAsync<InvalidOperationException>().WithMessage("*уже завершена*");
        legacy.Db.ChangeTracker.Clear();
        (await legacy.Db.Users.SingleAsync()).PasswordHash.Should().NotBeNull();
        (await legacy.Db.AdministratorSetupStates.SingleAsync()).IsCompleted.Should().BeTrue();
        await legacy.Db.AdministratorSetupStates.ExecuteDeleteAsync();
        legacy.Db.ChangeTracker.Clear();
        var plan = new Lab2UpgradePlan { RetireLegacyCredentials = true };
        await service.ApplyAsync(plan);
        legacy.Db.ChangeTracker.Clear();
        var retired = await legacy.Db.Users.SingleAsync();
        retired.PasswordHash.Should().BeNull();
        retired.IsActive.Should().BeFalse();
        var stamp = retired.SecurityStamp;
        legacy.Db.ChangeTracker.Clear();
        await service.ApplyAsync(plan);
        (await legacy.Db.Users.SingleAsync()).SecurityStamp.Should().Be(stamp);
    }

    private sealed class LegacyDatabase(PostgreSqlContainer postgres, HospitalDbContext db) : IAsyncDisposable
    {
        public HospitalDbContext Db { get; } = db;

        public static async Task<LegacyDatabase> CreateAsync()
        {
            var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await postgres.StartAsync();
            var db = new HospitalDbContext(new DbContextOptionsBuilder<HospitalDbContext>()
                .UseNpgsql(postgres.GetConnectionString()).Options);
            await db.GetService<IMigrator>().MigrateAsync("20261015165023_AddAccountsAndDirectoryConcurrency");
            return new LegacyDatabase(postgres, db);
        }

        public async Task<Guid> AddAccountAsync(string name, bool administrator = false)
        {
            var id = Guid.NewGuid();
            await Db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AspNetUsers" ("Id", "UserName", "NormalizedUserName", "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
                    "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
                VALUES ({id}, {name}, {name.ToUpperInvariant()}, 'legacy-test-hash', 'original-stamp', 'original-concurrency', false, false, false, true, 0)
                """);
            if (administrator)
            {
                var role = Guid.NewGuid();
                await Db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName")
                    VALUES ({role}, 'Administrator', 'ADMINISTRATOR') ON CONFLICT DO NOTHING;
                    INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
                    SELECT {id}, "Id" FROM "AspNetRoles" WHERE "Name" = 'Administrator';
                    """);
            }
            return id;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await postgres.DisposeAsync();
        }
    }
}
