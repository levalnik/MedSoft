using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalWorkers.Queries.GetMedicalWorkers;

public sealed class GetMedicalWorkersQueryHandler(IMedicalWorkerRepository repository)
    : IRequestHandler<GetMedicalWorkersQuery, IReadOnlyList<MedicalWorkerDto>>
{
    public async Task<IReadOnlyList<MedicalWorkerDto>> Handle(GetMedicalWorkersQuery request, CancellationToken cancellationToken) =>
        (await repository.ListAsync(request.IncludeDeleted, cancellationToken)).Select(x => x.ToDto()).ToArray();
}
