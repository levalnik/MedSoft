namespace HospitalInformationSystem.Contracts.Security;

public sealed record ConfirmSetupRequest(Guid ChallengeId, string Code);
