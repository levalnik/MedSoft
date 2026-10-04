using HospitalInformationSystem.Domain.Patients;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Riok.Mapperly.Abstractions;

namespace HospitalInformationSystem.Infrastructure.Persistence.Mappings;

[Mapper]
internal static partial class PatientDboMapper
{
    public static partial Patient ToDomain(this PatientDbo dbo);
    public static partial PatientDbo ToDbo(this Patient entity);

    [MapperIgnoreSource(nameof(Patient.Id))]
    [MapperIgnoreSource(nameof(Patient.CreatedAtUtc))]
    [MapperIgnoreTarget(nameof(PatientDbo.Id))]
    [MapperIgnoreTarget(nameof(PatientDbo.CreatedAtUtc))]
    public static partial void UpdateDbo(this Patient entity, PatientDbo dbo);
}

