using FluentValidation;

namespace HospitalInformationSystem.Application.MedicalWorkers.Rules;

internal static class MedicalWorkerRules
{
    public static IRuleBuilderOptions<T, string> WorkerText<T>(
        this IRuleBuilder<T, string> rule, string label, int minimumLength) => rule
        .Must(value => value is not null && value.Trim().Length >= minimumLength && value.Trim().Length <= 24)
        .WithMessage($"{label}: требуется от {minimumLength} до 24 символов.");
}
