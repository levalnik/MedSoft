using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Queries.GetMedicalRecordHistory;

public sealed class GetMedicalRecordHistoryQueryHandler(IMedicalRecordRepository repository)
    : IRequestHandler<GetMedicalRecordHistoryQuery, IReadOnlyList<MedicalRecordHistoryEventDto>>
{
    public async Task<IReadOnlyList<MedicalRecordHistoryEventDto>> Handle(GetMedicalRecordHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var record = await repository.FindAsync(request.Id, tracking: false, cancellationToken)
                     ?? throw new EntityNotFoundException("ЭМК не найдена.");
        return record.HistoryEvents.OrderBy(x => x.Version).Select(x => Mapping.MedicalRecordMapping.ToDto(x))
            .ToArray();
    }
}
