namespace HospitalInformationSystem.Contracts.MedicalRecords;

public sealed record MedicalRecordDiagnosisDto(
    Guid Id,
    string Code,
    string Name,
    DateTimeOffset AddedAtUtc,
    Guid AddedByMedicalWorkerId);
