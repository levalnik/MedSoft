using HospitalInformationSystem.Domain.Common;

namespace HospitalInformationSystem.Domain.Patients.Services;

public static class PatientService
{
    public static void Update(Patient patient, string firstName, string lastName, string? middleName,
        DateOnly birthDate, AdministrativeSex sex, string? comment, DateTimeOffset now)
    {
        var validFirstName = RequiredName(firstName, "Имя");
        var validLastName = RequiredName(lastName, "Фамилия");
        var validMiddleName = OptionalText(middleName);
        var validComment = OptionalText(comment);
        TextValidation.RejectNullCharacter(validMiddleName, "Отчество");
        TextValidation.RejectNullCharacter(validComment, "Комментарий");
        if (validMiddleName?.Length > 64)
            throw new DomainValidationException("Отчество: допускается не более 64 символов.");
        if (validComment?.Length > 32)
            throw new DomainValidationException("Комментарий: допускается не более 32 символов.");
        if (!Enum.IsDefined(sex))
            throw new DomainValidationException("Неизвестное значение пола.");

        patient.FirstName = validFirstName;
        patient.LastName = validLastName;
        patient.MiddleName = validMiddleName;
        patient.BirthDate = birthDate;
        patient.Sex = sex;
        patient.Comment = validComment;
        patient.UpdatedAtUtc = now.ToUniversalTime();
    }

    private static string RequiredName(string? value, string label)
    {
        var normalized = value?.Trim();
        TextValidation.RejectNullCharacter(normalized, label);
        if (normalized is null || normalized.Length is < 1 or > 64)
            throw new DomainValidationException($"{label}: требуется от 1 до 64 символов.");
        return normalized;
    }

    private static string? OptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
