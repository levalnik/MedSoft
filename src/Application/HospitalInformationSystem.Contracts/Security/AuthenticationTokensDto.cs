namespace HospitalInformationSystem.Contracts.Security;

public sealed record AuthenticationTokensDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    AuthenticationSessionDto Session);
