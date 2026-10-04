namespace HospitalInformationSystem.Contracts.MedicalRecords;

public sealed record MedicalRecordSnapshotDto(
    Guid MedicalRecordId,
    Guid PatientId,
    DateTimeOffset At,
    long Version,
    IReadOnlyCollection<MedicalRecordDiagnosisDto> Diagnoses,
    IReadOnlyCollection<PrescriptionDto> Prescriptions);
