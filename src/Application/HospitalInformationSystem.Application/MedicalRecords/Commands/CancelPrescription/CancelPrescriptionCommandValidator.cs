using FluentValidation;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.CancelPrescription;

public sealed class CancelPrescriptionCommandValidator : AbstractValidator<CancelPrescriptionCommand>
{
    public CancelPrescriptionCommandValidator()
    {
        RuleFor(x => x.MedicalRecordId).NotEmpty();
        RuleFor(x => x.PrescriptionId).NotEmpty();
        RuleFor(x => x.MedicalWorkerId).NotEmpty();
    }
}
