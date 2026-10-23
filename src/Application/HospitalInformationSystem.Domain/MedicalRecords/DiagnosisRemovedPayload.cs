namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed record DiagnosisRemovedPayload(Guid MedicalRecordDiagnosisId, string Code, string Name);
