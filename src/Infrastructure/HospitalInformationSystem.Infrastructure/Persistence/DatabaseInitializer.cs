using HospitalInformationSystem.Domain.MedicalWorkers;
using Microsoft.EntityFrameworkCore;

namespace HospitalInformationSystem.Infrastructure.Persistence;

public sealed class DatabaseInitializer(HospitalDbContext db, TimeProvider timeProvider)
{
    public async Task InitializeAsync(bool seed, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        if (!seed || await db.MedicalWorkers.AnyAsync(cancellationToken)) return;

        var now = timeProvider.GetUtcNow();
        var deleted = MedicalWorker.Create("Анна", "Кузнецова", "Медицинская сестра", now);
        deleted.MarkAsDeleted(now);
        db.MedicalWorkers.AddRange(
            MedicalWorker.Create("Алексей", "Иванов", "Врач-терапевт", now),
            MedicalWorker.Create("Мария", "Петрова", "Врач-хирург", now),
            MedicalWorker.Create("Дмитрий", "Смирнов", "Врач-анестезиолог", now),
            deleted);
        await db.SaveChangesAsync(cancellationToken);
    }
}
