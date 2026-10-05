namespace HospitalInformationSystem.Contracts.Patients;

public sealed record PatientDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    DateOnly BirthDate,
    AdministrativeSex Sex,
    string? Comment,
    bool HasMedicalRecord = false);
