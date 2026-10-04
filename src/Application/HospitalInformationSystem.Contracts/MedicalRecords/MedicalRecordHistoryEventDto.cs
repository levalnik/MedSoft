namespace HospitalInformationSystem.Contracts.MedicalRecords;

public sealed record MedicalRecordHistoryEventDto(
    long Version,
    DateTimeOffset OccurredAtUtc,
    Guid PerformedByMedicalWorkerId,
    string PerformedByName,
    string PerformedByPosition,
    string EventType,
    Guid? EntityId,
    string PayloadJson);
