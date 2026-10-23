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
