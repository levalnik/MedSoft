using HospitalInformationSystem.Domain.Common;

namespace HospitalInformationSystem.Domain.MedicalRecords;

public sealed class Prescription(
    Guid id,
    Guid medicalRecordId,
    string text,
    DateTimeOffset createdAtUtc,
    Guid createdByMedicalWorkerId,
    DateTimeOffset? cancelledAtUtc,
    Guid? cancelledByMedicalWorkerId,
    Guid? replacesPrescriptionId)
{
    public Guid Id { get; } = id;
    public Guid MedicalRecordId { get; } = medicalRecordId;
    public string Text { get; } = text;
    public DateTimeOffset CreatedAtUtc { get; } = createdAtUtc.ToUniversalTime();
    public Guid CreatedByMedicalWorkerId { get; } = createdByMedicalWorkerId;
    public DateTimeOffset? CancelledAtUtc { get; private set; } = cancelledAtUtc?.ToUniversalTime();
    public Guid? CancelledByMedicalWorkerId { get; private set; } = cancelledByMedicalWorkerId;
    public Guid? ReplacesPrescriptionId { get; } = replacesPrescriptionId;

    internal void Cancel(Guid authorId, DateTimeOffset at)
    {
        CancelledAtUtc = at;
        CancelledByMedicalWorkerId = authorId;
    }

    internal static string ValidateText(string? text)
    {
        var value = text?.Trim();
        if (value is null || value.Length is < 3 or > 128)
            throw new DomainValidationException("Назначение: требуется от 3 до 128 символов.");
        return value;
    }
}
