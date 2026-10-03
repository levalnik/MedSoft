using MediatR;

namespace HospitalInformationSystem.Application.MedicalWorkers.Commands.DeleteMedicalWorker;

public sealed record DeleteMedicalWorkerCommand(Guid Id) : IRequest;
