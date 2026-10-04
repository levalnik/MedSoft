using HospitalInformationSystem.Domain.Diagnoses;

namespace HospitalInformationSystem.Application.Abstractions;

public interface IDiagnosisRepository
{
    Task<IReadOnlyList<DiagnosisDefinition>> ListAsync(CancellationToken cancellationToken);
    Task<DiagnosisDefinition?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<bool> ExistsConflictAsync(string code, string name, Guid? excludingId, CancellationToken cancellationToken);
    void Add(DiagnosisDefinition entity);
    Task UpdateAsync(DiagnosisDefinition entity, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

