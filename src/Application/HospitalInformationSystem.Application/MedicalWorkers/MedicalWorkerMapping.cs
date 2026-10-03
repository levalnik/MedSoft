using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Domain.MedicalWorkers;

namespace HospitalInformationSystem.Application.MedicalWorkers;

internal static class MedicalWorkerMapping
{
    public static MedicalWorkerDto ToDto(this MedicalWorker worker) =>
        new(worker.Id, worker.FirstName, worker.LastName, worker.Position, worker.IsDeleted);
}
