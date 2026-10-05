using HospitalInformationSystem.Domain.MedicalRecords.Factories;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.MedicalRecords;
using HospitalInformationSystem.Domain.MedicalRecords;
using MediatR;

namespace HospitalInformationSystem.Application.MedicalRecords.Commands.CreateMedicalRecord;

public sealed class CreateMedicalRecordCommandHandler(
    MedicalRecordCommandContext context,
    IPatientRepository patients,
    IMedicalRecordPresence presence
) : IRequestHandler<CreateMedicalRecordCommand, MedicalRecordDto>
{
    public async Task<MedicalRecordDto> Handle(CreateMedicalRecordCommand request, CancellationToken cancellationToken)
    {
        var author = await context.RequireAuthorAsync(request.MedicalWorkerId, cancellationToken);
        _ = await patients.FindAsync(request.PatientId, tracking: false, cancellationToken)
            ?? throw new EntityNotFoundException("Пациент не найден.");
        if (await presence.ExistsForPatientAsync(request.PatientId, cancellationToken))
            throw new BusinessConflictException("У пациента уже есть ЭМК.");
        var record = MedicalRecordFactory.Create(request.PatientId, author, context.Now);
        await context.SaveNewAsync(record, cancellationToken);
        return Mapping.MedicalRecordMapping.ToDto(record);
    }
}
