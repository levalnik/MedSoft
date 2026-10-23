namespace HospitalInformationSystem.Contracts.Security;

public sealed record AccountDetailsDto(
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
