namespace HospitalInformationSystem.Infrastructure.Persistence.Models;

public sealed class AuditEventDbo
{
    public Guid EventId { get; set; }
    public DateTimeOffset EventTimeUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ActorKind { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string ActorDisplayName { get; set; } = string.Empty;
    public string? RequestId { get; set; }
    public string? Component { get; set; }
    public string Message { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
}
