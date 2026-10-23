using FluentValidation;
using HospitalInformationSystem.Application.MedicalWorkers.Rules;

namespace HospitalInformationSystem.Application.MedicalWorkers.Commands.UpdateMedicalWorker;

public sealed class UpdateMedicalWorkerCommandValidator : AbstractValidator<UpdateMedicalWorkerCommand>
{
    public UpdateMedicalWorkerCommandValidator()
    {
        RuleFor(x => x.FirstName).WorkerText("Имя", 1);
        RuleFor(x => x.LastName).WorkerText("Фамилия", 1);
        RuleFor(x => x.Position).WorkerText("Должность", 3);
        RuleFor(x => x.Role).IsInEnum().WithMessage("Неизвестная медицинская роль.");
    }
}
