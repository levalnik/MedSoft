namespace HospitalInformationSystem.Web.Development;

internal sealed record MockAuditEvent(Guid EventId, DateTimeOffset EventTime, string EventType,
    Guid? UserId, string DisplayName, string Message, Guid? TargetId, bool IsSystem = false);
