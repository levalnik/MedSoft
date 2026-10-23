namespace HospitalInformationSystem.Contracts.Auth;

public sealed record LoginRequest(string UserName, string Password, string? TotpCode = null,
    LoginPortal? Portal = null);

public sealed record AntiforgeryTokenDto(string RequestToken);

public sealed record SessionDto(Guid Id, string UserName, string Role, Guid? MedicalWorkerId,
    HospitalInformationSystem.Contracts.Lab2.MedicalWorkerRole? MedicalWorkerRole = null);

public sealed record AccountDto(Guid Id, string UserName, string Role, Guid? MedicalWorkerId, bool IsLockedOut,
    bool IsActive = true, DateTimeOffset CreatedAtUtc = default, DateTimeOffset UpdatedAtUtc = default);

public sealed record CreateAccountRequest(string UserName, string Password, string Role, Guid? MedicalWorkerId);
