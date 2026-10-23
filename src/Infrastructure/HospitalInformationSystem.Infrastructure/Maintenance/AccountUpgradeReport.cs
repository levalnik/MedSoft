namespace HospitalInformationSystem.Infrastructure.Maintenance;

public sealed record AccountUpgradeReport(Guid Id, string? UserName, string[] Roles,
    bool RequiresRename, bool HasLegacyPassword);
