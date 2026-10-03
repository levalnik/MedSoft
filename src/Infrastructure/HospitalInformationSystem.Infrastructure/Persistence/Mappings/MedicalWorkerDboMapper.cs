using HospitalInformationSystem.Domain.MedicalWorkers;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Riok.Mapperly.Abstractions;

namespace HospitalInformationSystem.Infrastructure.Persistence.Mappings;

[Mapper]
internal static partial class MedicalWorkerDboMapper
{
    public static partial MedicalWorker ToDomain(this MedicalWorkerDbo dbo);

    public static partial MedicalWorkerDbo ToDbo(this MedicalWorker worker);

    // Обновляем тот же DBO, который отслеживает EF, сохраняя ключ и дату создания.
    [MapperIgnoreSource(nameof(MedicalWorker.Id))]
    [MapperIgnoreSource(nameof(MedicalWorker.CreatedAtUtc))]
    [MapperIgnoreTarget(nameof(MedicalWorkerDbo.Id))]
    [MapperIgnoreTarget(nameof(MedicalWorkerDbo.CreatedAtUtc))]
    public static partial void UpdateDbo(this MedicalWorker worker, MedicalWorkerDbo dbo);
}
