using FluentValidation;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.AddPrescription;

public sealed class AddPrescriptionCommandValidator : AbstractValidator<AddPrescriptionCommand>
{
    public AddPrescriptionCommandValidator()
    {
        RuleFor(x => x.MedicalRecordId).NotEmpty();
        RuleFor(x => x.Text).Must(text => text?.Trim().Length is >= 3 and <= 128)
            .WithMessage("Назначение должно содержать от 3 до 128 символов.");
        RuleFor(x => x.MedicalWorkerId).NotEmpty();
    }
}
