using System.Security.Claims;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Domain.Common;
using HospitalInformationSystem.Domain.MedicalWorkers;
using HospitalInformationSystem.Infrastructure.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HospitalInformationSystem.Infrastructure.Identity;

public sealed class AccountService(
    HospitalDbContext db,
    UserManager<HospitalUser> users,
    TimeProvider timeProvider) : IAccountService
{
    public async Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return await (from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            orderby user.UserName
            select new AccountDto(user.Id, user.UserName!, role.Name!, user.MedicalWorkerId,
                user.LockoutEnd != null && user.LockoutEnd > now, user.IsActive,
                user.CreatedAtUtc, user.UpdatedAtUtc)).ToArrayAsync(cancellationToken);
    }

    public async Task<AccountDto> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var name = request.UserName?.Trim();
        if (!AccountUserNameRules.HasValidLength(name))
            throw new DomainValidationException("Логин должен содержать от 3 до 16 символов.");
        if (request.Password is null || request.Password.Length is < 12 or > 128)
            throw new DomainValidationException("Пароль должен содержать от 12 до 128 символов.");
        if (request.Role is not (ApplicationRoles.Administrator or ApplicationRoles.MedicalWorker))
            throw new DomainValidationException("Неизвестная роль учётной записи.");
        if (request.Role == ApplicationRoles.Administrator && request.MedicalWorkerId is not null)
            throw new DomainValidationException("Для администратора нельзя указывать медработника.");
        if (request.Role == ApplicationRoles.MedicalWorker &&
            (request.MedicalWorkerId is not { } workerId ||
             !await db.MedicalWorkers.AnyAsync(x => x.Id == workerId && !x.IsDeleted, cancellationToken)))
            throw new DomainValidationException("Выберите действующего медработника.");

        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        if (request.Role == ApplicationRoles.Administrator &&
            await db.Users.AnyAsync(account => account.IsAdministrator, cancellationToken))
            throw new BusinessConflictException("В системе уже существует администратор.");
        var now = timeProvider.GetUtcNow();
        var user = new HospitalUser
        {
            Id = Guid.NewGuid(), UserName = name, MedicalWorkerId = request.MedicalWorkerId,
            LockoutEnabled = true, IsAdministrator = request.Role == ApplicationRoles.Administrator,
            IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        RequireSuccess(await users.CreateAsync(user, request.Password));
        RequireSuccess(await users.AddToRoleAsync(user, request.Role));
        if (user.MedicalWorkerId is { } medicalWorkerId)
            RequireSuccess(await users.AddClaimAsync(user,
                new Claim(AuthClaimTypes.MedicalWorkerId, medicalWorkerId.ToString())));
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return new AccountDto(user.Id, name!, request.Role, user.MedicalWorkerId, false,
            user.IsActive, user.CreatedAtUtc, user.UpdatedAtUtc);
    }

    public async Task<SessionDto?> GetSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || user.UserName is null || user.LockoutEnd > timeProvider.GetUtcNow())
            return null;
        var roles = await (from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == user.Id
            select role.Name).ToArrayAsync(cancellationToken);
        if (roles.Length != 1 || roles[0] is not (ApplicationRoles.Administrator or ApplicationRoles.MedicalWorker) ||
            user.IsAdministrator != (roles[0] == ApplicationRoles.Administrator))
            return null;
        HospitalInformationSystem.Contracts.Lab2.MedicalWorkerRole? medicalRole = null;
        if (roles[0] == ApplicationRoles.MedicalWorker)
        {
            if (user.MedicalWorkerId is not { } workerId)
                return null;
            var worker = await db.MedicalWorkers.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == workerId && !x.IsDeleted, cancellationToken);
            if (worker is null)
                return null;
            medicalRole = worker.Role switch
            {
                MedicalWorkerRole.Doctor => HospitalInformationSystem.Contracts.Lab2.MedicalWorkerRole.Doctor,
                MedicalWorkerRole.LaboratoryTechnician => HospitalInformationSystem.Contracts.Lab2.MedicalWorkerRole.LaboratoryTechnician,
                MedicalWorkerRole.Nurse => HospitalInformationSystem.Contracts.Lab2.MedicalWorkerRole.Nurse,
                MedicalWorkerRole.Other => HospitalInformationSystem.Contracts.Lab2.MedicalWorkerRole.Other,
                _ => null
            };
            if (medicalRole is null)
                return null;
        }
        else if (user.MedicalWorkerId is not null)
            return null;
        return new SessionDto(user.Id, user.UserName, roles[0]!, user.MedicalWorkerId, medicalRole);
    }

    private static void RequireSuccess(IdentityResult result)
    {
        if (result.Succeeded) return;
        if (result.Errors.Any(x => x.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
            throw new BusinessConflictException("Этот логин уже используется.");
        throw new DomainValidationException(string.Join(" ", result.Errors.Select(x => x.Description)));
    }
}
