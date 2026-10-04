using FluentValidation;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.RemoveMedicalRecordDiagnosis;

public sealed class RemoveMedicalRecordDiagnosisCommandValidator : AbstractValidator<RemoveMedicalRecordDiagnosisCommand>
{
    public RemoveMedicalRecordDiagnosisCommandValidator()
    {
        RuleFor(x => x.MedicalRecordId).NotEmpty();
        RuleFor(x => x.MedicalRecordDiagnosisId).NotEmpty();
        RuleFor(x => x.MedicalWorkerId).NotEmpty();
    }
}
