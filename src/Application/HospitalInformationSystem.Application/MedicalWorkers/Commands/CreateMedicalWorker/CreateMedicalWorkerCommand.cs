using HospitalInformationSystem.Contracts.MedicalWorkers;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalWorkers.Commands.CreateMedicalWorker;

public sealed record CreateMedicalWorkerCommand(string FirstName, string LastName, string Position,
    Contracts.Security.MedicalWorkerRole? Role = null)
    : IRequest<MedicalWorkerDto>;
