using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.CancelPrescription;

public sealed class CancelPrescriptionCommandHandler(MedicalRecordCommandContext context)
    : IRequestHandler<CancelPrescriptionCommand>
{
    public async Task Handle(CancelPrescriptionCommand request, CancellationToken cancellationToken)
    {
        var author = await context.RequireAuthorAsync(request.MedicalWorkerId, cancellationToken);
        var record = await context.RequireRecordAsync(request.MedicalRecordId, cancellationToken);
        record.CancelPrescription(request.PrescriptionId, author, context.Now);
        await context.SaveChangesAsync(record, cancellationToken);
    }
}
