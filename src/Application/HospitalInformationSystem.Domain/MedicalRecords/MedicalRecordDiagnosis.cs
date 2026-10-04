namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed class MedicalRecordDiagnosis
{
    public Guid Id { get; init; }
    public Guid MedicalRecordId { get; init; }
    public Guid? DiagnosisDefinitionId { get; init; }
    public required string DiagnosisCodeSnapshot { get; init; }
    public required string DiagnosisNameSnapshot { get; init; }
    public DateTimeOffset AddedAtUtc { get; init; }
    public Guid AddedByMedicalWorkerId { get; init; }
    public DateTimeOffset? RemovedAtUtc { get; set; }
    public Guid? RemovedByMedicalWorkerId { get; set; }
}
