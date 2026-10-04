using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.RemoveMedicalRecordDiagnosis;

public sealed class RemoveMedicalRecordDiagnosisCommandHandler(MedicalRecordCommandContext context)
    : IRequestHandler<RemoveMedicalRecordDiagnosisCommand>
{
    public async Task Handle(RemoveMedicalRecordDiagnosisCommand request, CancellationToken cancellationToken)
    {
        var author = await context.RequireAuthorAsync(request.MedicalWorkerId, cancellationToken);
        var record = await context.RequireRecordAsync(request.MedicalRecordId, cancellationToken);
        record.RemoveDiagnosis(request.MedicalRecordDiagnosisId, author, context.Now);
        await context.SaveChangesAsync(record, cancellationToken);
    }
}
