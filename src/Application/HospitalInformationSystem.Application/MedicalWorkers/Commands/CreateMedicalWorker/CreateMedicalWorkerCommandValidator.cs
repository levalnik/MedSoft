using FluentValidation;
using HospitalInformationSystem.Application.MedicalWorkers.Rules;

namespace HospitalInformationSystem.Application.MedicalWorkers.Commands.CreateMedicalWorker;

public sealed class CreateMedicalWorkerCommandValidator : AbstractValidator<CreateMedicalWorkerCommand>
{
    public CreateMedicalWorkerCommandValidator()
    {
        RuleFor(x => x.FirstName).WorkerText("Имя");
        RuleFor(x => x.LastName).WorkerText("Фамилия");
        RuleFor(x => x.Position).WorkerText("Должность");
    }
}
