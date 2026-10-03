using HospitalInformationSystem.Domain.MedicalWorkers;

namespace HospitalInformationSystem.Application.Abstractions;

public interface IMedicalWorkerRepository
{
    Task<IReadOnlyList<MedicalWorker>> ListAsync(bool includeDeleted, CancellationToken cancellationToken);
    Task<MedicalWorker?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    void Add(MedicalWorker worker);
}
