namespace HospitalInformationSystem.Contracts.Security;

public sealed record ResetAccountPasswordResultDto(
    Guid AccountId,
    string GeneratedPassword,
    int RevokedRefreshTokenCount);
