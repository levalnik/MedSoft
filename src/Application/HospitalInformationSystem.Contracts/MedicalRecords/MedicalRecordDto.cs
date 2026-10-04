namespace HospitalInformationSystem.Contracts.MedicalRecords;

public sealed record MedicalRecordDto(
    Guid Id,
    Guid PatientId,
    DateTimeOffset CreatedAtUtc,
    long Version,
    IReadOnlyCollection<MedicalRecordDiagnosisDto> Diagnoses,
    IReadOnlyCollection<PrescriptionDto> Prescriptions);
