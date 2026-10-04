using HospitalInformationSystem.Domain.Common;

namespace HospitalInformationSystem.Domain.Patients;

public sealed class Patient
{
    public Patient(Guid id, string firstName, string lastName, string? middleName,
        DateOnly birthDate, AdministrativeSex sex, string? comment,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Update(firstName, lastName, middleName, birthDate, sex, comment, updatedAtUtc);
    }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? MiddleName { get; private set; }
    public DateOnly BirthDate { get; private set; }
    public AdministrativeSex Sex { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Patient Create(string firstName, string lastName, string? middleName,
        DateOnly birthDate, AdministrativeSex sex, string? comment, DateTimeOffset now) =>
        new(Guid.NewGuid(), firstName, lastName, middleName, birthDate, sex, comment, now, now);

    public void Update(string firstName, string lastName, string? middleName,
        DateOnly birthDate, AdministrativeSex sex, string? comment, DateTimeOffset now)
    {
        var validFirstName = RequiredName(firstName, "Имя");
        var validLastName = RequiredName(lastName, "Фамилия");
        var validMiddleName = OptionalText(middleName);
        var validComment = OptionalText(comment);
        if (validMiddleName?.Length > 64)
            throw new DomainValidationException("Отчество: допускается не более 64 символов.");
        if (validComment?.Length > 32)
            throw new DomainValidationException("Комментарий: допускается не более 32 символов.");
        if (!Enum.IsDefined(sex))
            throw new DomainValidationException("Неизвестное значение пола.");

        FirstName = validFirstName;
        LastName = validLastName;
        MiddleName = validMiddleName;
        BirthDate = birthDate;
        Sex = sex;
        Comment = validComment;
        UpdatedAtUtc = now.ToUniversalTime();
    }

    private static string RequiredName(string? value, string label)
    {
        var normalized = value?.Trim();
        if (normalized is null || normalized.Length is < 1 or > 64)
            throw new DomainValidationException($"{label}: требуется от 1 до 64 символов.");
        return normalized;
    }

    private static string? OptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

