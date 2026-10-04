using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Contracts.Patients;
using MediatR;

namespace HospitalInformationSystem.Application.Patients.Queries.GetPatients;

public sealed class GetPatientsQueryHandler(IPatientRepository repository, IMedicalRecordPresence recordPresence)
    : IRequestHandler<GetPatientsQuery, IReadOnlyList<PatientDto>>
{
    public async Task<IReadOnlyList<PatientDto>> Handle(GetPatientsQuery request, CancellationToken cancellationToken)
    {
        var patients = await repository.ListAsync(cancellationToken);
        var idsWithRecords = await recordPresence.GetPatientIdsWithRecordsAsync(
            patients.Select(x => x.Id).ToArray(), cancellationToken);
        return patients.Select(patient => patient.ToDto() with
        {
            HasMedicalRecord = idsWithRecords.Contains(patient.Id)
        }).ToArray();
    }
}

