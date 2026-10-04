namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed class MedicalRecordHistoryEvent(
    Guid id,
    Guid medicalRecordId,
    long version,
    DateTimeOffset occurredAtUtc,
    Guid performedByMedicalWorkerId,
    string performedByNameSnapshot,
    string performedByPositionSnapshot,
    MedicalRecordEventType eventType,
    Guid? entityId,
    string payloadJson)
{
    public Guid Id { get; } = id;
    public Guid MedicalRecordId { get; } = medicalRecordId;
    public long Version { get; } = version;
    public DateTimeOffset OccurredAtUtc { get; } = occurredAtUtc.ToUniversalTime();
    public Guid PerformedByMedicalWorkerId { get; } = performedByMedicalWorkerId;
    public string PerformedByNameSnapshot { get; } = performedByNameSnapshot;
    public string PerformedByPositionSnapshot { get; } = performedByPositionSnapshot;
    public MedicalRecordEventType EventType { get; } = eventType;
    public Guid? EntityId { get; } = entityId;
    public string PayloadJson { get; } = payloadJson;
}
