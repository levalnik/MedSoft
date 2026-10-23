using HospitalInformationSystem.Contracts.MedicalWorkers;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalWorkers.Commands.CreateMedicalWorker;

public sealed record CreateMedicalWorkerCommand(string FirstName, string LastName, string Position,
    Contracts.Lab2.MedicalWorkerRole? Role = null)
    : IRequest<MedicalWorkerDto>;
