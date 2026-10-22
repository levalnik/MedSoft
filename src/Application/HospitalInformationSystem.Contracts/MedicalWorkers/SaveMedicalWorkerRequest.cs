namespace HospitalInformationSystem.Contracts.MedicalWorkers;

public sealed record SaveMedicalWorkerRequest(string FirstName, string LastName, string Position,
    Lab2.MedicalWorkerRole? Role = null);
