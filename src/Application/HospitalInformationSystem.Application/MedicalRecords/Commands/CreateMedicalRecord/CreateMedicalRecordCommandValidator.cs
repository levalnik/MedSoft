using FluentValidation;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.CreateMedicalRecord;

public sealed class CreateMedicalRecordCommandValidator : AbstractValidator<CreateMedicalRecordCommand>
{
    public CreateMedicalRecordCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.MedicalWorkerId).NotEmpty();
    }
}
