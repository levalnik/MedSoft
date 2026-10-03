using FluentValidation;

namespace HospitalInformationSystem.Application.MedicalWorkers.Commands.UpdateMedicalWorker;

public sealed class UpdateMedicalWorkerCommandValidator : AbstractValidator<UpdateMedicalWorkerCommand>
{
    public UpdateMedicalWorkerCommandValidator()
    {
        RuleFor(x => x.FirstName).WorkerText("Имя");
        RuleFor(x => x.LastName).WorkerText("Фамилия");
        RuleFor(x => x.Position).WorkerText("Должность");
    }
}
