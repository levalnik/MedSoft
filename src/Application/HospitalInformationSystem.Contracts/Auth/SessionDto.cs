namespace HospitalInformationSystem.Contracts.Auth;

public sealed record SessionDto(Guid Id, string UserName, string Role, Guid? MedicalWorkerId,
    HospitalInformationSystem.Contracts.Security.MedicalWorkerRole? MedicalWorkerRole = null);
