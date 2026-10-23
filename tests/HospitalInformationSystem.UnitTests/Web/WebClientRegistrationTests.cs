using System.Threading;
using System.Net;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Security;
using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Configuration;
using HospitalInformationSystem.Web.Development;
using HospitalInformationSystem.Web.Gateways;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class WebClientRegistrationTests
{
    [Fact]
    public async Task AddWebClient_MockConfiguration_ShouldWorkWithoutApiAddress()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["UiMock:Enabled"] = "true" }).Build();
        var services = new ServiceCollection().AddHospitalWebClients(configuration, "UiMock");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolved = scope.ServiceProvider;

        using var anonymous = await resolved.GetRequiredService<HttpClient>().GetAsync("api/auth/session");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resolved.GetRequiredService<HttpClient>().BaseAddress!.Host.Should().Be("ui-mock.invalid");
        resolved.GetRequiredService<UiMockState>().SwitchPersona(UiMockPersona.Doctor);
        var session = await resolved.GetRequiredService<IAuthenticationGateway>().GetSessionAsync(CancellationToken.None);
        session!.MedicalWorkerRole.Should().Be(MedicalWorkerRole.Doctor);
        var workers = await resolved.GetRequiredService<MedicalWorkersApiClient>()
            .ListAsync(false, CancellationToken.None);
        workers.Should().NotBeEmpty();
        resolved.GetRequiredService<IAccountManagementGateway>().Should().NotBeNull();
        resolved.GetRequiredService<IAuditGateway>().Should().NotBeNull();
        resolved.GetRequiredService<IMedicalRecordsGateway>().Should().NotBeNull();
    }

    [Fact]
    public void AddWebClient_NormalConfiguration_ShouldOmitMockStateAndTransport()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ApiBaseUrl"] = "https://api.example.invalid/" }).Build();
        var services = new ServiceCollection().AddHospitalWebClients(configuration, "Production");
        using var provider = services.BuildServiceProvider();

        provider.GetService<UiMockState>().Should().BeNull();
        provider.GetService<UiMockHttpMessageHandler>().Should().BeNull();
        provider.GetRequiredService<HttpClient>().BaseAddress.Should().Be(new Uri("https://api.example.invalid/"));
    }
}
