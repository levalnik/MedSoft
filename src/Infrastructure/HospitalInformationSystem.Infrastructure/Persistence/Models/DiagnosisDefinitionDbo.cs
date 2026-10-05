namespace HospitalInformationSystem.Infrastructure.Persistence.Models;

public sealed class DiagnosisDefinitionDbo
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
