namespace HospitalInformationSystem.Infrastructure.Persistence.Models;

public sealed class PrescriptionDbo
{
    public Guid Id { get; set; }
    public Guid MedicalRecordId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid CreatedByMedicalWorkerId { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public Guid? CancelledByMedicalWorkerId { get; set; }
    public Guid? ReplacesPrescriptionId { get; set; }
}

