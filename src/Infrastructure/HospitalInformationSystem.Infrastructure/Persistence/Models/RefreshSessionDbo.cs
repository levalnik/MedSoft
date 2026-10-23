namespace HospitalInformationSystem.Infrastructure.Persistence.Models;

public sealed class RefreshSessionDbo
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
    public string? RevokeReason { get; set; }
}
