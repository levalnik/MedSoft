using HospitalInformationSystem.Domain.Common;

namespace HospitalInformationSystem.Domain.MedicalWorkers.Services;

public static class MedicalWorkerService
{
    public static void Update(MedicalWorker worker, string firstName, string lastName, string position,
        DateTimeOffset now, MedicalWorkerRole? role = null)
    {
        var validFirstName = Validate(firstName, "Имя", 1);
        var validLastName = Validate(lastName, "Фамилия", 1);
        var validPosition = Validate(position, "Должность", 3);
        var validRole = role ?? worker.Role;
        if (!Enum.IsDefined(validRole))
            throw new DomainValidationException("Неизвестная медицинская роль.");
        worker.FirstName = validFirstName;
        worker.LastName = validLastName;
        worker.Position = validPosition;
        worker.Role = validRole;
        worker.UpdatedAtUtc = now.ToUniversalTime();
    }

    public static void MarkAsDeleted(MedicalWorker worker, DateTimeOffset now)
    {
        if (worker.IsDeleted) return;
        worker.IsDeleted = true;
        worker.UpdatedAtUtc = now.ToUniversalTime();
    }

    private static string Validate(string? value, string label, int minimumLength)
    {
        var normalized = value?.Trim();
        TextValidation.RejectNullCharacter(normalized, label);
        if (normalized is null || normalized.Length < minimumLength || normalized.Length > 24)
            throw new DomainValidationException($"{label}: требуется от {minimumLength} до 24 символов.");
        return normalized;
    }
}
