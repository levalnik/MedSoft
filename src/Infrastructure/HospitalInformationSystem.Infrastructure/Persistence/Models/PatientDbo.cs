namespace HospitalInformationSystem.Infrastructure.Persistence.Models;

public sealed class PatientDbo
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public DateOnly BirthDate { get; set; }
    public int Sex { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

