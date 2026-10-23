using HospitalInformationSystem.Contracts.MedicalRecords;

namespace HospitalInformationSystem.Web.Development;

internal sealed class MockMedicalRecord(MedicalRecordDto current)
{
    public MedicalRecordDto Current { get; set; } = current;
    public List<MedicalRecordHistoryEventDto> History { get; } = [];
    public List<(DateTimeOffset At, MedicalRecordDto State)> States { get; } = [];
}
