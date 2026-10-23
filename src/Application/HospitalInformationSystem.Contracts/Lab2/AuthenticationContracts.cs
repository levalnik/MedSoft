namespace HospitalInformationSystem.Contracts.Lab2;

public sealed record AuthenticationSessionDto(
    Guid Id,
    string UserName,
    string AccountRole,
    Guid? MedicalWorkerId,
    MedicalWorkerRole? MedicalWorkerRole);

public sealed record SetupStatusDto(bool SetupRequired);

public sealed record BeginSetupRequest(string Password, string ConfirmPassword);

public sealed record SetupChallengeDto(
    Guid ChallengeId,
    string SecretKey,
    string AuthenticatorUri,
    DateTimeOffset ExpiresAt);

public sealed record ConfirmSetupRequest(Guid ChallengeId, string Code);

public sealed record AuthenticationTokensDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    AuthenticationSessionDto Session);
