using HospitalInformationSystem.Domain.Common;

namespace HospitalInformationSystem.Domain.MedicalRecords.Factories;

public static class PrescriptionFactory
{
    public static Prescription Create(
        Guid medicalRecordId, string text, Guid authorId, DateTimeOffset at, Guid? replacesPrescriptionId = null) =>
        new()
        {
            Id = Guid.NewGuid(), MedicalRecordId = medicalRecordId, Text = NormalizeText(text),
            CreatedAtUtc = at.ToUniversalTime(), CreatedByMedicalWorkerId = authorId,
            ReplacesPrescriptionId = replacesPrescriptionId
        };

    internal static string NormalizeText(string? text)
    {
        var value = text?.Trim();
        TextValidation.RejectNullCharacter(value, "Назначение");
        if (value is null || value.Length is < 3 or > 128)
            throw new DomainValidationException("Назначение: требуется от 3 до 128 символов.");
        return value;
    }
}
