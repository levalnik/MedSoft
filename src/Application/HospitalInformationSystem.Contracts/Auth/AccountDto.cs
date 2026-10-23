namespace HospitalInformationSystem.Contracts.Auth;

public sealed record AccountDto(Guid Id, string UserName, string Role, Guid? MedicalWorkerId, bool IsLockedOut,
    bool IsActive = true, DateTimeOffset CreatedAtUtc = default, DateTimeOffset UpdatedAtUtc = default);
