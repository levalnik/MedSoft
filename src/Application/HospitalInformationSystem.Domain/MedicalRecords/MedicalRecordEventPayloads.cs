namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed record MedicalRecordCreatedPayload(Guid PatientId);

public sealed record DiagnosisAddedPayload(
    Guid MedicalRecordDiagnosisId, Guid DiagnosisDefinitionId, string Code, string Name);

public sealed record DiagnosisRemovedPayload(Guid MedicalRecordDiagnosisId, string Code, string Name);

public sealed record PrescriptionAddedPayload(Guid PrescriptionId, string Text, Guid? ReplacesPrescriptionId);

public sealed record PrescriptionCancelledPayload(Guid PrescriptionId, string Text);
