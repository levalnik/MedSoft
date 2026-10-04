using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.AddMedicalRecordDiagnosis;

public sealed class AddMedicalRecordDiagnosisCommandHandler(MedicalRecordCommandContext context, IDiagnosisRepository diagnoses)
    : IRequestHandler<AddMedicalRecordDiagnosisCommand, MedicalRecordDiagnosisDto>
{
    public async Task<MedicalRecordDiagnosisDto> Handle(AddMedicalRecordDiagnosisCommand request, CancellationToken cancellationToken)
    {
        var author = await context.RequireAuthorAsync(request.MedicalWorkerId, cancellationToken);
        var record = await context.RequireRecordAsync(request.MedicalRecordId, cancellationToken);
        var definition = await diagnoses.FindAsync(request.DiagnosisId, tracking: false, cancellationToken)
            ?? throw new EntityNotFoundException("Диагноз справочника не найден.");
        var result = record.AddDiagnosis(definition.Id, definition.Code, definition.Name, author, context.Now);
        await context.SaveChangesAsync(record, cancellationToken);
        return result.ToDto();
    }
}
