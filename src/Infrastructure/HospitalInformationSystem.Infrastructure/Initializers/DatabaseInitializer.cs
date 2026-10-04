using HospitalInformationSystem.Domain.MedicalWorkers.Factories;
using HospitalInformationSystem.Domain.MedicalWorkers.Services;
using HospitalInformationSystem.Domain.MedicalWorkers;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace HospitalInformationSystem.Infrastructure.Initializers;

public sealed class DatabaseInitializer(HospitalDbContext db, TimeProvider timeProvider)
{
    public async Task InitializeAsync(bool seed, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        if (!seed || await db.MedicalWorkers.AnyAsync(cancellationToken)) return;

        var now = timeProvider.GetUtcNow();
        var deleted = MedicalWorkerFactory.Create("Анна", "Кузнецова", "Медицинская сестра", now);
        MedicalWorkerService.MarkAsDeleted(deleted, now);
        db.MedicalWorkers.AddRange(
            MedicalWorkerFactory.Create("Алексей", "Иванов", "Врач-терапевт", now).ToDbo(),
            MedicalWorkerFactory.Create("Мария", "Петрова", "Врач-хирург", now).ToDbo(),
            MedicalWorkerFactory.Create("Дмитрий", "Смирнов", "Врач-анестезиолог", now).ToDbo(),
            deleted.ToDbo());
        await db.SaveChangesAsync(cancellationToken);
    }
}
