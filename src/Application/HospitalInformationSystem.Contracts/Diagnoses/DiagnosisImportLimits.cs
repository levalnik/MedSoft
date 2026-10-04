namespace HospitalInformationSystem.Contracts.Diagnoses;

public static class DiagnosisImportLimits
{
    public const int MaxFileBytes = 1024 * 1024;
    public const int MaxRows = 10_000;
}
