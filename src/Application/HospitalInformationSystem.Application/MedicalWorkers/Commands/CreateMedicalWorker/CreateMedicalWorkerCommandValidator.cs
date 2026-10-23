using FluentValidation;
using HospitalInformationSystem.Application.MedicalWorkers.Rules;

namespace HospitalInformationSystem.Application.MedicalWorkers.Commands.CreateMedicalWorker;

public sealed class CreateMedicalWorkerCommandValidator : AbstractValidator<CreateMedicalWorkerCommand>
{
    public CreateMedicalWorkerCommandValidator()
    {
        RuleFor(x => x.FirstName).WorkerText("Имя", 1);
        RuleFor(x => x.LastName).WorkerText("Фамилия", 1);
        RuleFor(x => x.Position).WorkerText("Должность", 3);
        RuleFor(x => x.Role).IsInEnum().WithMessage("Неизвестная медицинская роль.");
    }
}
