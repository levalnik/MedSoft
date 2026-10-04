using HospitalInformationSystem.Contracts.Patients;
using MediatR;

namespace HospitalInformationSystem.Application.Patients.Queries.GetPatient;

public sealed record GetPatientQuery(Guid Id) : IRequest<PatientDto>;

