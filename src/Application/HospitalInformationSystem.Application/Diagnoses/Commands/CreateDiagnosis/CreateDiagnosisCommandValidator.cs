using FluentValidation;

namespace HospitalInformationSystem.Application.Diagnoses.Commands.CreateDiagnosis;

public sealed class CreateDiagnosisCommandValidator : AbstractValidator<CreateDiagnosisCommand>
{
    public CreateDiagnosisCommandValidator()
    {
        RuleFor(x => x.Code).Must(value => value?.Trim().Length == 6)
            .WithMessage("Код диагноза: требуется ровно 6 символов.");
        RuleFor(x => x.Name).Must(value => value is not null && value.Trim().Length is >= 3 and <= 24)
            .WithMessage("Название диагноза: требуется от 3 до 24 символов.");
    }
}

