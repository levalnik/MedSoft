using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Infrastructure.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HospitalInformationSystem.Infrastructure.Identity;

public sealed class IdentityBootstrapper(
    HospitalDbContext db,
    RoleManager<IdentityRole<Guid>> roles,
    UserManager<HospitalUser> users,
    AccountService accounts)
{
    public async Task InitializeAsync(string? userName, string? password,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1296385107, 1)", cancellationToken);
        foreach (var role in new[] { ApplicationRoles.Administrator, ApplicationRoles.MedicalWorker })
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await roles.RoleExistsAsync(role)) continue;
            var result = await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() });
            if (!result.Succeeded)
                throw new InvalidOperationException("Не удалось создать роли приложения.");
        }

        if ((await users.GetUsersInRoleAsync(ApplicationRoles.Administrator)).Count == 0)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException(
                    "Для первой учётной записи настройте Authentication:BootstrapAdmin:UserName и Password через локальные секреты или переменные окружения.");
            await accounts.CreateAsync(new CreateAccountRequest(userName, password,
                ApplicationRoles.Administrator, null), cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
