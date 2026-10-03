using HospitalInformationSystem.Contracts.MedicalWorkers;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalWorkers.Queries.GetMedicalWorkers;

public sealed record GetMedicalWorkersQuery(bool IncludeDeleted = false) : IRequest<IReadOnlyList<MedicalWorkerDto>>;
