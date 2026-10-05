using HospitalInformationSystem.Domain.Common;

namespace HospitalInformationSystem.Domain.MedicalWorkers.Services;

public static class MedicalWorkerService
{
    public static void Update(MedicalWorker worker, string firstName, string lastName, string position, DateTimeOffset now)
    {
        var validFirstName = Validate(firstName, "Имя");
        var validLastName = Validate(lastName, "Фамилия");
        var validPosition = Validate(position, "Должность");
        worker.FirstName = validFirstName;
        worker.LastName = validLastName;
        worker.Position = validPosition;
        worker.UpdatedAtUtc = now.ToUniversalTime();
    }

    public static void MarkAsDeleted(MedicalWorker worker, DateTimeOffset now)
    {
        if (worker.IsDeleted) return;
        worker.IsDeleted = true;
        worker.UpdatedAtUtc = now.ToUniversalTime();
    }

    private static string Validate(string? value, string label)
    {
        var normalized = value?.Trim();
        TextValidation.RejectNullCharacter(normalized, label);
        if (normalized is null || normalized.Length is < 3 or > 24)
            throw new DomainValidationException($"{label}: требуется от 3 до 24 символов.");
        return normalized;
    }
}
