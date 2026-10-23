using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Domain.MedicalWorkers;
using Riok.Mapperly.Abstractions;

namespace HospitalInformationSystem.Application.MedicalWorkers.Mapping;

[Mapper]
internal static partial class MedicalWorkerMapping
{
    [MapperIgnoreSource(nameof(MedicalWorker.CreatedAtUtc))]
    [MapperIgnoreSource(nameof(MedicalWorker.UpdatedAtUtc))]
    [MapperIgnoreTarget(nameof(MedicalWorkerDto.Role))]
    public static partial MedicalWorkerDto ToDto(this MedicalWorker worker);
}
