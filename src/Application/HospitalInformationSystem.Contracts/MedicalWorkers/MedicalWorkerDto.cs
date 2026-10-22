namespace HospitalInformationSystem.Contracts.MedicalWorkers;

public sealed record MedicalWorkerDto(Guid Id, string FirstName, string LastName, string Position, bool IsDeleted,
    Lab2.MedicalWorkerRole? Role = null);
