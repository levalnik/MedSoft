namespace HospitalInformationSystem.Domain.Patients;

public sealed class Patient
{
    public Guid Id { get; init; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? MiddleName { get; set; }
    public DateOnly BirthDate { get; set; }
    public AdministrativeSex Sex { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
