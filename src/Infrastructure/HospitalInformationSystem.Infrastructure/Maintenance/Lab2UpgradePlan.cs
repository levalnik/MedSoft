namespace HospitalInformationSystem.Infrastructure.Maintenance;

public sealed record Lab2UpgradePlan
{
    public int Version { get; init; } = 1;
    public Dictionary<Guid, string> UserNames { get; init; } = [];
    public Dictionary<Guid, string> MedicalWorkerRoles { get; init; } = [];
    public Guid[] RetiredAdministrators { get; init; } = [];
    public bool RetireLegacyCredentials { get; init; }
}

public sealed record Lab2AccountUpgradeReport(Guid Id, string? UserName, string[] Roles,
    bool RequiresRename, bool HasLegacyPassword);

public sealed record Lab2UpgradeReport(IReadOnlyList<Lab2AccountUpgradeReport> Accounts,
    IReadOnlyList<Guid> MedicalWorkerIds, bool RequiresAdministratorSelection);
