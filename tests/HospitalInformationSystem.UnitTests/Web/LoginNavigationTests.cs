using FluentAssertions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Web.Components.Authentication;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class LoginNavigationTests
{
    [Theory]
    [InlineData("/admin/accounts?status=active", "/admin/login")]
    [InlineData("/medical/patients", "/login")]
    [InlineData("/", "/login")]
    public void Protected_routes_choose_the_corresponding_login_page(string requested, string expected)
    {
        LoginNavigation.GetLoginPath(requested).Should().Be(expected);
    }

    [Theory]
    [InlineData(LoginPortal.Administrator, "/admin/accounts?status=active")]
    [InlineData(LoginPortal.MedicalWorker, "/medical/patients?search=test")]
    public void Login_can_return_to_a_local_page_in_the_same_portal(LoginPortal portal, string requested)
    {
        LoginNavigation.GetReturnPath(portal, requested).Should().Be(requested);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://attacker.example/admin/accounts")]
    [InlineData("//attacker.example/admin/accounts")]
    [InlineData("/medical/patients")]
    [InlineData("/admin/../medical/patients")]
    [InlineData("/admin/%2e%2e/medical/patients")]
    [InlineData("/admin/%2e%2e%2fmedical/patients")]
    [InlineData("/admin/\\attacker.example")]
    [InlineData("/admin/%5cattacker.example")]
    [InlineData("/admin/accounts\r\nLocation: https://attacker.example")]
    [InlineData("/admin/login")]
    public void Invalid_return_paths_fall_back_to_the_portal_home(string? requested)
    {
        LoginNavigation.GetReturnPath(LoginPortal.Administrator, requested).Should().Be("/admin/medical-workers");
    }
}
