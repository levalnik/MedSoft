namespace HospitalInformationSystem.Domain.Diagnoses;

public sealed class DiagnosisDefinition
{
    public Guid Id { get; init; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
