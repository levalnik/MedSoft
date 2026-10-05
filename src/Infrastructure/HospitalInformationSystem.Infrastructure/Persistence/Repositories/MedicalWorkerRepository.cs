using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Domain.MedicalWorkers;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace HospitalInformationSystem.Infrastructure.Persistence.Repositories;

public sealed class MedicalWorkerRepository(HospitalDbContext db) : IMedicalWorkerRepository
{
    public async Task<IReadOnlyList<MedicalWorker>> ListAsync(bool includeDeleted, CancellationToken cancellationToken)
    {
        var rows = await db.MedicalWorkers.AsNoTracking()
            .Where(x => includeDeleted || !x.IsDeleted)
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(x => x.ToDomain()).ToArray();
    }

    public async Task<MedicalWorker?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.MedicalWorkers : db.MedicalWorkers.AsNoTracking();
        var row = await query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row?.ToDomain();
    }

    public void Add(MedicalWorker worker) => db.MedicalWorkers.Add(worker.ToDbo());

    public async Task UpdateAsync(MedicalWorker worker, CancellationToken cancellationToken)
    {
        var row = await db.MedicalWorkers.FindAsync([worker.Id], cancellationToken)
                  ?? throw new EntityNotFoundException("Медработник не найден.");
        worker.UpdateDbo(row);
    }
}
