using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Domain.Diagnoses;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Persistence.Mappings;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalInformationSystem.Infrastructure.Persistence.Repositories;

public sealed class DiagnosisRepository(HospitalDbContext db) : IDiagnosisRepository
{
    public async Task<IReadOnlyList<DiagnosisDefinition>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Set<DiagnosisDefinitionDbo>().AsNoTracking()
            .OrderBy(x => x.Code).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(x => x.ToDomain()).ToArray();
    }

    public async Task<IReadOnlyList<DiagnosisDefinition>> FindImportMatchesAsync(
        IReadOnlyCollection<string> codes, IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        var rows = await db.DiagnosisDefinitions.AsTracking()
            .Where(x => codes.Contains(x.Code) || names.Contains(x.Name))
            .ToListAsync(cancellationToken);
        return rows.Select(x => x.ToDomain()).ToArray();
    }

    public async Task<DiagnosisDefinition?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Set<DiagnosisDefinitionDbo>() : db.Set<DiagnosisDefinitionDbo>().AsNoTracking();
        var row = await query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row?.ToDomain();
    }

    public Task<bool> ExistsConflictAsync(string code, string name, Guid? excludingId,
        CancellationToken cancellationToken) =>
        db.Set<DiagnosisDefinitionDbo>().AsNoTracking().AnyAsync(
            x => (!excludingId.HasValue || x.Id != excludingId.Value) && (x.Code == code || x.Name == name),
            cancellationToken);

    public void Add(DiagnosisDefinition entity) => db.Set<DiagnosisDefinitionDbo>().Add(entity.ToDbo());

    public async Task UpdateAsync(DiagnosisDefinition entity, CancellationToken cancellationToken)
    {
        var row = await db.Set<DiagnosisDefinitionDbo>().FindAsync([entity.Id], cancellationToken)
                  ?? throw new EntityNotFoundException("Диагноз не найден.");
        entity.UpdateDbo(row);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Set<DiagnosisDefinitionDbo>().FindAsync([id], cancellationToken)
                  ?? throw new EntityNotFoundException("Диагноз не найден.");
        db.Set<DiagnosisDefinitionDbo>().Remove(row);
    }
}
