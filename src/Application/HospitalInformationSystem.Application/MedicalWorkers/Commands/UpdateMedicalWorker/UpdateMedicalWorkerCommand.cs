using HospitalInformationSystem.Contracts.MedicalWorkers;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalWorkers.Commands.UpdateMedicalWorker;

public sealed record UpdateMedicalWorkerCommand(Guid Id, string FirstName, string LastName, string Position,
    Contracts.Security.MedicalWorkerRole? Role = null)
    : IRequest<MedicalWorkerDto>;
