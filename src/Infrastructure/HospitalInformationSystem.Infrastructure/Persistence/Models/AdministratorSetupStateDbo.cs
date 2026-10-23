namespace HospitalInformationSystem.Infrastructure.Persistence.Models;

public sealed class AdministratorSetupStateDbo
{
    public int Id { get; set; } = 1;
    public Guid? AdministratorUserId { get; set; }
    public bool IsCompleted { get; set; }
    public string? TotpSecretProtected { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}
