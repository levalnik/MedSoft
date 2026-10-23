namespace HospitalInformationSystem.Contracts.Security;

public sealed record SetAccountActiveResultDto(AccountDetailsDto Account, int RevokedRefreshTokenCount);
