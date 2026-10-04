namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed class MedicalRecordHistoryEvent
{
    public Guid Id { get; init; }
    public Guid MedicalRecordId { get; init; }
    public long Version { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
    public Guid PerformedByMedicalWorkerId { get; init; }
    public required string PerformedByNameSnapshot { get; init; }
    public required string PerformedByPositionSnapshot { get; init; }
    public MedicalRecordEventType EventType { get; init; }
    public Guid? EntityId { get; init; }
    public required string PayloadJson { get; init; }
}
