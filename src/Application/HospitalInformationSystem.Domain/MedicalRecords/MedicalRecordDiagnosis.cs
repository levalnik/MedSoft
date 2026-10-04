namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed class MedicalRecordDiagnosis(
    Guid id,
    Guid medicalRecordId,
    Guid? diagnosisDefinitionId,
    string diagnosisCodeSnapshot,
    string diagnosisNameSnapshot,
    DateTimeOffset addedAtUtc,
    Guid addedByMedicalWorkerId,
    DateTimeOffset? removedAtUtc,
    Guid? removedByMedicalWorkerId)
{
    public Guid Id { get; } = id;
    public Guid MedicalRecordId { get; } = medicalRecordId;
    public Guid? DiagnosisDefinitionId { get; } = diagnosisDefinitionId;
    public string DiagnosisCodeSnapshot { get; } = diagnosisCodeSnapshot;
    public string DiagnosisNameSnapshot { get; } = diagnosisNameSnapshot;
    public DateTimeOffset AddedAtUtc { get; } = addedAtUtc.ToUniversalTime();
    public Guid AddedByMedicalWorkerId { get; } = addedByMedicalWorkerId;
    public DateTimeOffset? RemovedAtUtc { get; private set; } = removedAtUtc?.ToUniversalTime();
    public Guid? RemovedByMedicalWorkerId { get; private set; } = removedByMedicalWorkerId;

    internal void Remove(Guid authorId, DateTimeOffset at)
    {
        RemovedAtUtc = at;
        RemovedByMedicalWorkerId = authorId;
    }
}
