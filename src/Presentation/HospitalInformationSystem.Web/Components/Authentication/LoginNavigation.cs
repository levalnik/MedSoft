using HospitalInformationSystem.Contracts.Auth;

namespace HospitalInformationSystem.Web.Components.Authentication;

public static class LoginNavigation
{
    private static readonly Uri LocalOrigin = new("https://medsoft.invalid/");

    public static string GetLoginPath(string requestedPath) =>
        requestedPath.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase) ? "/admin/login" : "/login";

    public static string GetReturnPath(LoginPortal portal, string? requestedPath)
    {
        var administrator = portal == LoginPortal.Administrator;
        var prefix = administrator ? "/admin/" : "/medical/";
        var home = administrator ? "/admin/medical-workers" : "/medical/patients";

        if (string.IsNullOrEmpty(requestedPath) || !requestedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            requestedPath.Contains('\\') || requestedPath.Any(char.IsControl) ||
            !Uri.TryCreate(LocalOrigin, requestedPath, out var target) ||
            target.Authority != LocalOrigin.Authority ||
            !target.LocalPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || target.LocalPath.Contains('\\') ||
            target.LocalPath.Split('/').Any(segment => segment is "." or "..") ||
            target.AbsolutePath.Equals("/admin/login", StringComparison.OrdinalIgnoreCase))
            return home;

        return requestedPath;
    }
}
