using HospitalInformationSystem.Domain.Patients;

namespace HospitalInformationSystem.Application.Abstractions;

public interface IPatientRepository
{
    Task<IReadOnlyList<Patient>> ListAsync(CancellationToken cancellationToken);
    Task<Patient?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    void Add(Patient entity);
    Task UpdateAsync(Patient entity, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

