using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Queries.GetMedicalRecordSnapshot;

public sealed class GetMedicalRecordSnapshotQueryHandler(IMedicalRecordSnapshotBuilder builder)
    : IRequestHandler<GetMedicalRecordSnapshotQuery, MedicalRecordSnapshotDto>
{
    public async Task<MedicalRecordSnapshotDto> Handle(GetMedicalRecordSnapshotQuery request, CancellationToken cancellationToken)
    {
        return await builder.BuildAsync(request.Id, request.At, cancellationToken);
    }
}
