using System.Text.Json;
using HospitalInformationSystem.Domain.MedicalWorkers;

namespace HospitalInformationSystem.Domain.MedicalRecords.Factories;

public static class MedicalRecordHistoryEventFactory
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public static MedicalRecordHistoryEvent Create<TPayload>(
        Guid medicalRecordId, long version, MedicalRecordEventType type,
        Guid? entityId, TPayload payload, MedicalWorker author, DateTimeOffset at) =>
        new()
        {
            Id = Guid.NewGuid(), MedicalRecordId = medicalRecordId, Version = version,
            OccurredAtUtc = at.ToUniversalTime(), PerformedByMedicalWorkerId = author.Id,
            PerformedByNameSnapshot = $"{author.LastName} {author.FirstName}",
            PerformedByPositionSnapshot = author.Position, EventType = type, EntityId = entityId,
            PayloadJson = JsonSerializer.Serialize(payload, PayloadOptions)
        };
}
