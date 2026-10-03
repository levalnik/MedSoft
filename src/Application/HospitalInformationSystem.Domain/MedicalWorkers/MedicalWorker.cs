using HospitalInformationSystem.Domain.Common;

namespace HospitalInformationSystem.Domain.MedicalWorkers;

public sealed class MedicalWorker
{
    private MedicalWorker() { }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Position { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static MedicalWorker Create(
        string firstName, string lastName, string position, DateTimeOffset now)
    {
        var worker = new MedicalWorker { Id = Guid.NewGuid(), CreatedAtUtc = now.ToUniversalTime() };
        worker.Update(firstName, lastName, position, now);
        return worker;
    }

    public void Update(string firstName, string lastName, string position, DateTimeOffset now)
    {
        var validFirstName = Validate(firstName, "Имя");
        var validLastName = Validate(lastName, "Фамилия");
        var validPosition = Validate(position, "Должность");
        FirstName = validFirstName;
        LastName = validLastName;
        Position = validPosition;
        UpdatedAtUtc = now.ToUniversalTime();
    }

    public void MarkAsDeleted(DateTimeOffset now)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        UpdatedAtUtc = now.ToUniversalTime();
    }

    private static string Validate(string? value, string label)
    {
        var normalized = value?.Trim();
        if (normalized is null || normalized.Length is < 3 or > 24)
            throw new DomainValidationException($"{label}: требуется от 3 до 24 символов.");
        return normalized;
    }
}
