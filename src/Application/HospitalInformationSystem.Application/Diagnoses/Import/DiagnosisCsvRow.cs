namespace HospitalInformationSystem.Application.Diagnoses.Import;

public sealed record DiagnosisCsvRow(long Line, string Code, string Name);
