namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed record DiagnosisAddedPayload(
    Guid MedicalRecordDiagnosisId,
    Guid DiagnosisDefinitionId,
    string Code,
    string Name);
