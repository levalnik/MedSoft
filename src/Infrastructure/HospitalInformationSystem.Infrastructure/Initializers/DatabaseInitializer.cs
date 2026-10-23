using HospitalInformationSystem.Domain.Diagnoses.Factories;
using HospitalInformationSystem.Domain.MedicalWorkers.Factories;
using HospitalInformationSystem.Domain.MedicalWorkers;
using HospitalInformationSystem.Domain.MedicalWorkers.Services;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace HospitalInformationSystem.Infrastructure.Initializers;

public sealed class DatabaseInitializer(HospitalDbContext db, TimeProvider timeProvider)
{
    private static readonly (string Code, string Name)[] DemoDiagnoses =
    [
        ("A00.00", "Холера"),
        ("B01.00", "Ветряная оспа"),
        ("E11.90", "Диабет 2 типа"),
        ("I10.00", "Гипертензия")
    ];

    public async Task InitializeAsync(bool seed, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        if (!seed)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        await SeedMedicalWorkersAsync(now, cancellationToken);
        await SeedDiagnosesAsync(now, cancellationToken);
    }

    private async Task SeedMedicalWorkersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await db.MedicalWorkers.AnyAsync(cancellationToken))
        {
            return;
        }

        var deleted = MedicalWorkerFactory.Create("Анна", "Кузнецова", "Медицинская сестра", now, MedicalWorkerRole.Nurse);
        MedicalWorkerService.MarkAsDeleted(deleted, now);
        db.MedicalWorkers.AddRange(
            MedicalWorkerFactory.Create("Алексей", "Иванов", "Врач-терапевт", now, MedicalWorkerRole.Doctor).ToDbo(),
            MedicalWorkerFactory.Create("Мария", "Петрова", "Врач-хирург", now, MedicalWorkerRole.Doctor).ToDbo(),
            MedicalWorkerFactory.Create("Дмитрий", "Смирнов", "Врач-анестезиолог", now, MedicalWorkerRole.Doctor).ToDbo(),
            deleted.ToDbo());
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDiagnosesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existingCodes = (await db.DiagnosisDefinitions.AsNoTracking()
            .Select(x => x.Code).ToListAsync(cancellationToken)).ToHashSet();
        var existingNames = (await db.DiagnosisDefinitions.AsNoTracking()
            .Select(x => x.Name).ToListAsync(cancellationToken)).ToHashSet();

        var toAdd = DemoDiagnoses
            .Where(diagnosis => !existingCodes.Contains(diagnosis.Code) && !existingNames.Contains(diagnosis.Name))
            .Select(diagnosis => DiagnosisDefinitionFactory.Create(diagnosis.Code, diagnosis.Name, now).ToDbo())
            .ToArray();
        if (toAdd.Length == 0)
        {
            return;
        }

        db.DiagnosisDefinitions.AddRange(toAdd);
        await db.SaveChangesAsync(cancellationToken);
    }
}
