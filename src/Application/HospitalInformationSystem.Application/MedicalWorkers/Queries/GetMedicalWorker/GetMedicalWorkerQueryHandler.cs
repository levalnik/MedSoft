using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalWorkers.Queries.GetMedicalWorker;

public sealed class GetMedicalWorkerQueryHandler(IMedicalWorkerRepository repository)
    : IRequestHandler<GetMedicalWorkerQuery, MedicalWorkerDto>
{
    public async Task<MedicalWorkerDto> Handle(GetMedicalWorkerQuery request, CancellationToken cancellationToken)
    {
        var worker = await repository.FindAsync(request.Id, tracking: false, cancellationToken)
            ?? throw new EntityNotFoundException("Медработник не найден.");
        return worker.ToDto();
    }
}
