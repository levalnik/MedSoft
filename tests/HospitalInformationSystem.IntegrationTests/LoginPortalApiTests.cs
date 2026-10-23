using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class LoginPortalApiTests(SecureHospitalApiFactory factory) : IClassFixture<SecureHospitalApiFactory>
{
    [Theory]
    [InlineData(LoginPortal.Administrator, HttpStatusCode.OK)]
    [InlineData(LoginPortal.MedicalWorker, HttpStatusCode.Unauthorized)]
    public async Task The_selected_portal_does_not_change_the_administrators_actual_role(
        LoginPortal portal, HttpStatusCode expected)
    {
        using var client = factory.CreateCookieClient();
        await SetCsrfAsync(client);

        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            SecureHospitalApiFactory.AdminUserName, SecureHospitalApiFactory.AdminPassword, Portal: portal));

        response.StatusCode.Should().Be(expected);
        using var session = await client.GetAsync("/api/auth/session");
        session.StatusCode.Should().Be(expected);
        if (expected == HttpStatusCode.OK)
            (await session.Content.ReadFromJsonAsync<SessionDto>())!.Role.Should().Be(ApplicationRoles.Administrator);
        else
            AssertNoSessionCookie(response);
    }

    [Fact]
    public async Task A_medical_worker_cannot_enter_the_administrator_portal_with_a_valid_password()
    {
        using var administrator = factory.CreateCookieClient();
        await SetCsrfAsync(administrator);
        using var login = await administrator.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            SecureHospitalApiFactory.AdminUserName, SecureHospitalApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        await SetCsrfAsync(administrator);
        using var createdWorker = await administrator.PostAsJsonAsync("/api/medical-workers",
            new SaveMedicalWorkerRequest("Алексей", "Проверочный", "Врач"));
        createdWorker.EnsureSuccessStatusCode();
        var worker = (await createdWorker.Content.ReadFromJsonAsync<MedicalWorkerDto>())!;
        const string password = "Integration.Portal-Only.6249!";
        var name = "portal." + Guid.NewGuid().ToString("N");
        using var createdAccount = await administrator.PostAsJsonAsync("/api/accounts",
            new CreateAccountRequest(name, password, ApplicationRoles.MedicalWorker, worker.Id));
        createdAccount.EnsureSuccessStatusCode();
        using var client = factory.CreateCookieClient();
        await SetCsrfAsync(client);

        using var rejected = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(name, password, Portal: LoginPortal.Administrator));

        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AssertNoSessionCookie(rejected);
        using var anonymous = await client.GetAsync("/api/auth/session");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var accepted = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(name, password, Portal: LoginPortal.MedicalWorker));
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await accepted.Content.ReadFromJsonAsync<SessionDto>())!.Role.Should().Be(ApplicationRoles.MedicalWorker);
    }

    [Fact]
    public async Task The_cookie_backend_does_not_silently_accept_an_unverified_totp_code()
    {
        using var client = factory.CreateCookieClient();
        await SetCsrfAsync(client);

        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            SecureHospitalApiFactory.AdminUserName, SecureHospitalApiFactory.AdminPassword,
            TotpCode: "123456", Portal: LoginPortal.Administrator));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AssertNoSessionCookie(response);
        using var session = await client.GetAsync("/api/auth/session");
        session.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task SetCsrfAsync(HttpClient client)
    {
        var token = (await client.GetFromJsonAsync<AntiforgeryTokenDto>("/api/auth/csrf"))!;
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.RequestToken);
    }

    private static void AssertNoSessionCookie(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            cookies.Should().NotContain(cookie => cookie.Contains("MedSoft.Session=", StringComparison.Ordinal));
    }
}
