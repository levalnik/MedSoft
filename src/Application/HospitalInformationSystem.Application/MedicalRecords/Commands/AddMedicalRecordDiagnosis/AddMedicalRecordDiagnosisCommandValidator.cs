using FluentValidation;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.AddMedicalRecordDiagnosis;

public sealed class AddMedicalRecordDiagnosisCommandValidator : AbstractValidator<AddMedicalRecordDiagnosisCommand>
{
    public AddMedicalRecordDiagnosisCommandValidator()
    {
        RuleFor(x => x.MedicalRecordId).NotEmpty();
        RuleFor(x => x.DiagnosisId).NotEmpty();
        RuleFor(x => x.MedicalWorkerId).NotEmpty();
    }
}
