using FluentValidation;

namespace HospitalInformationSystem.Application.MedicalWorkers.Rules;

internal static class MedicalWorkerRules
{
    public static IRuleBuilderOptions<T, string> WorkerText<T>(
        this IRuleBuilder<T, string> rule, string label) => rule
        .Must(value => value is not null && value.Trim().Length is >= 3 and <= 24)
        .WithMessage($"{label}: требуется от 3 до 24 символов.");
}
