namespace HospitalInformationSystem.Web.Development;

internal sealed record MockAccount(Guid Id, string UserName, string Role, Guid? MedicalWorkerId,
    bool IsActive, bool IsLockedOut, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
