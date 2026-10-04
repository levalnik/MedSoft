using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.ReplacePrescription;

public sealed class ReplacePrescriptionCommandHandler(MedicalRecordCommandContext context)
    : IRequestHandler<ReplacePrescriptionCommand, PrescriptionDto>
{
    public async Task<PrescriptionDto> Handle(ReplacePrescriptionCommand request, CancellationToken cancellationToken)
    {
        var author = await context.RequireAuthorAsync(request.MedicalWorkerId, cancellationToken);
        var record = await context.RequireRecordAsync(request.MedicalRecordId, cancellationToken);
        var result = record.ReplacePrescription(request.PrescriptionId, request.Text, author, context.Now);
        await context.SaveChangesAsync(record, cancellationToken);
        return result.ToDto();
    }
}
