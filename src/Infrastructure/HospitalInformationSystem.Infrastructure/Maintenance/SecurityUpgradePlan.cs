namespace HospitalInformationSystem.Infrastructure.Maintenance;

public sealed record SecurityUpgradePlan
{
    public int Version { get; init; } = 1;
    public Dictionary<Guid, string> UserNames { get; init; } = [];
    public Dictionary<Guid, string> MedicalWorkerRoles { get; init; } = [];
    public Guid[] RetiredAdministrators { get; init; } = [];
    public bool RetireLegacyCredentials { get; init; }
}
