using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Domain.Patients;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Persistence.Mappings;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalInformationSystem.Infrastructure.Persistence.Repositories;

public sealed class PatientRepository(HospitalDbContext db) : IPatientRepository
{
    public async Task<IReadOnlyList<Patient>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Set<PatientDbo>().AsNoTracking()
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(x => x.ToDomain()).ToArray();
    }

    public async Task<Patient?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Set<PatientDbo>() : db.Set<PatientDbo>().AsNoTracking();
        var row = await query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row?.ToDomain();
    }

    public void Add(Patient entity) => db.Set<PatientDbo>().Add(entity.ToDbo());

    public async Task UpdateAsync(Patient entity, CancellationToken cancellationToken)
    {
        var row = await db.Set<PatientDbo>().FindAsync([entity.Id], cancellationToken)
            ?? throw new EntityNotFoundException("Пациент не найден.");
        entity.UpdateDbo(row);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Set<PatientDbo>().FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Пациент не найден.");
        db.Set<PatientDbo>().Remove(row);
    }
}

