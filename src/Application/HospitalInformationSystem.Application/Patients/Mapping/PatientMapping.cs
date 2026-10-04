using HospitalInformationSystem.Contracts.Patients;
using HospitalInformationSystem.Domain.Patients;
using Riok.Mapperly.Abstractions;

namespace HospitalInformationSystem.Application.Patients;

[Mapper]
internal static partial class PatientMapping
{
    [MapperIgnoreSource(nameof(Patient.CreatedAtUtc))]
    [MapperIgnoreSource(nameof(Patient.UpdatedAtUtc))]
    [MapperIgnoreTarget(nameof(PatientDto.HasMedicalRecord))]
    public static partial PatientDto ToDto(this Patient patient);

    public static partial Domain.Patients.AdministrativeSex ToDomain(
        this Contracts.Patients.AdministrativeSex sex);
}

