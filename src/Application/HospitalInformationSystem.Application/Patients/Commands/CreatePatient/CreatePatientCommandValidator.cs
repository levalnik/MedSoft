using FluentValidation;

namespace HospitalInformationSystem.Application.Patients.Commands.CreatePatient;

public sealed class CreatePatientCommandValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientCommandValidator()
    {
        RuleFor(x => x.FirstName).Must(value => value is not null && value.Trim().Length is >= 1 and <= 64)
            .WithMessage("Имя: требуется от 1 до 64 символов.");
        RuleFor(x => x.LastName).Must(value => value is not null && value.Trim().Length is >= 1 and <= 64)
            .WithMessage("Фамилия: требуется от 1 до 64 символов.");
        RuleFor(x => x.MiddleName).Must(value => value is null || value.Trim().Length <= 64)
            .WithMessage("Отчество: допускается не более 64 символов.");
        RuleFor(x => x.Comment).Must(value => value is null || value.Trim().Length <= 32)
            .WithMessage("Комментарий: допускается не более 32 символов.");
        RuleFor(x => x.Sex).IsInEnum().WithMessage("Неизвестное значение пола.");
    }
}
