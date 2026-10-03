using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Domain.MedicalWorkers;
using Microsoft.EntityFrameworkCore;

namespace HospitalInformationSystem.Infrastructure.Persistence.Repositories;

public sealed class MedicalWorkerRepository(HospitalDbContext db) : IMedicalWorkerRepository
{
    public async Task<IReadOnlyList<MedicalWorker>> ListAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        await db.MedicalWorkers.AsNoTracking()
            .Where(x => includeDeleted || !x.IsDeleted)
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<MedicalWorker?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.MedicalWorkers : db.MedicalWorkers.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public void Add(MedicalWorker worker) => db.MedicalWorkers.Add(worker);
}
