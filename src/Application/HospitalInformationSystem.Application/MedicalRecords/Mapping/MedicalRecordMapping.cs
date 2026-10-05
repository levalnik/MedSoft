using HospitalInformationSystem.Contracts.MedicalRecords;
using HospitalInformationSystem.Domain.MedicalRecords;
using Riok.Mapperly.Abstractions;

namespace HospitalInformationSystem.Application.MedicalRecords.Mapping;

[Mapper]
public static partial class MedicalRecordMapping
{
    [MapperIgnoreSource(nameof(MedicalRecord.HistoryEvents))]
    public static partial MedicalRecordDto ToDto(this MedicalRecord record);

    [MapProperty(nameof(MedicalRecordDiagnosis.DiagnosisCodeSnapshot), nameof(MedicalRecordDiagnosisDto.Code))]
    [MapProperty(nameof(MedicalRecordDiagnosis.DiagnosisNameSnapshot), nameof(MedicalRecordDiagnosisDto.Name))]
    [MapperIgnoreSource(nameof(MedicalRecordDiagnosis.MedicalRecordId))]
    [MapperIgnoreSource(nameof(MedicalRecordDiagnosis.DiagnosisDefinitionId))]
    [MapperIgnoreSource(nameof(MedicalRecordDiagnosis.RemovedAtUtc))]
    [MapperIgnoreSource(nameof(MedicalRecordDiagnosis.RemovedByMedicalWorkerId))]
    public static partial MedicalRecordDiagnosisDto ToDto(this MedicalRecordDiagnosis diagnosis);

    [MapperIgnoreSource(nameof(Prescription.MedicalRecordId))]
    [MapperIgnoreSource(nameof(Prescription.CancelledAtUtc))]
    [MapperIgnoreSource(nameof(Prescription.CancelledByMedicalWorkerId))]
    public static partial PrescriptionDto ToDto(this Prescription prescription);

    [MapProperty(nameof(MedicalRecordHistoryEvent.PerformedByNameSnapshot), nameof(MedicalRecordHistoryEventDto.PerformedByName))]
    [MapProperty(nameof(MedicalRecordHistoryEvent.PerformedByPositionSnapshot), nameof(MedicalRecordHistoryEventDto.PerformedByPosition))]
    [MapperIgnoreSource(nameof(MedicalRecordHistoryEvent.Id))]
    [MapperIgnoreSource(nameof(MedicalRecordHistoryEvent.MedicalRecordId))]
    public static partial MedicalRecordHistoryEventDto ToDto(this MedicalRecordHistoryEvent historyEvent);

    private static IReadOnlyCollection<MedicalRecordDiagnosisDto> MapCurrentDiagnoses(
        IReadOnlyCollection<MedicalRecordDiagnosis> diagnoses) =>
        diagnoses.Where(x => x.RemovedAtUtc is null).Select(x => x.ToDto()).ToArray();

    private static IReadOnlyCollection<PrescriptionDto> MapCurrentPrescriptions(
        IReadOnlyCollection<Prescription> prescriptions) =>
        prescriptions.Where(x => x.CancelledAtUtc is null).Select(x => x.ToDto()).ToArray();
}
