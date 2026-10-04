namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed class Prescription
{
    public Guid Id { get; init; }
    public Guid MedicalRecordId { get; init; }
    public required string Text { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public Guid CreatedByMedicalWorkerId { get; init; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public Guid? CancelledByMedicalWorkerId { get; set; }
    public Guid? ReplacesPrescriptionId { get; init; }
}
