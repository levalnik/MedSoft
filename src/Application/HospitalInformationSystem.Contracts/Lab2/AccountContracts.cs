namespace HospitalInformationSystem.Contracts.Lab2;

public sealed record Lab2AccountDto(
    Guid Id,
    string UserName,
    string AccountRole,
    Guid? MedicalWorkerId,
    string? MedicalWorkerName,
    MedicalWorkerRole? MedicalWorkerRole,
    bool IsActive,
    bool IsLockedOut,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateWorkerAccountRequest(string UserName);

public sealed record CreateWorkerAccountResultDto(Lab2AccountDto Account, string GeneratedPassword);

public sealed record ResetAccountPasswordResultDto(
    Guid AccountId,
    string GeneratedPassword,
    int RevokedRefreshTokenCount);

public sealed record SetAccountActiveRequest(bool IsActive);

public sealed record SetAccountActiveResultDto(Lab2AccountDto Account, int RevokedRefreshTokenCount);
