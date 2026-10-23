namespace HospitalInformationSystem.Contracts.Security;

public sealed record CreateWorkerAccountResultDto(AccountDetailsDto Account, string GeneratedPassword);
