using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net.Http;
using System.Net.Http.Json;
using HospitalInformationSystem.Contracts.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HospitalInformationSystem.IntegrationTests;

internal static class BusinessTestAuthentication
{
    public const string Scheme = "BusinessTests";

    public static HttpClient CreateBusinessClient(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var csrf = client.GetFromJsonAsync<AntiforgeryTokenDto>("/api/auth/csrf").GetAwaiter().GetResult()!;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.RequestToken);
        return client;
    }

    public static void AddBusinessTestAuthentication(this IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services => services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = Scheme;
                options.DefaultChallengeScheme = Scheme;
                options.DefaultForbidScheme = Scheme;
            })
            .AddScheme<AuthenticationSchemeOptions, BusinessTestAuthenticationHandler>(Scheme, _ => { }));
}

internal sealed class BusinessTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "77830f7e-596f-46f1-9089-8c2c59134a8b"),
            new(ClaimTypes.Name, "business-test-user"),
            new(ClaimTypes.Role, ApplicationRoles.Administrator),
            new(ClaimTypes.Role, ApplicationRoles.MedicalWorker)
        };
        if (Guid.TryParse(Request.Headers["X-Medical-Worker-Id"].ToString(), out var authorId))
            claims.Add(new Claim(AuthClaimTypes.MedicalWorkerId, authorId.ToString()));
        var identity = new ClaimsIdentity(claims, BusinessTestAuthentication.Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identity), BusinessTestAuthentication.Scheme)));
    }
}
