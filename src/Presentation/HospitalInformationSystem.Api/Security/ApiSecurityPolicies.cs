namespace HospitalInformationSystem.Api.Security;

public static class ApiSecurityPolicies
{
    public const string Administrator = "Administrator";
    public const string Clinical = "Clinical";
    public const string CatalogRead = "CatalogRead";
    public const string LoginRateLimit = "Login";
}
