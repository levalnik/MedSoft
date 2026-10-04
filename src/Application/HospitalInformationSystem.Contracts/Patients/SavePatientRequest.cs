namespace HospitalInformationSystem.Contracts.Patients;

public sealed record SavePatientRequest(
    string FirstName, string LastName, string? MiddleName,
    DateOnly BirthDate, AdministrativeSex Sex, string? Comment);

