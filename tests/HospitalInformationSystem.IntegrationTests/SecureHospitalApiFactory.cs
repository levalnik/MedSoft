using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class SecureHospitalApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUserName = "integration.admin";
    public const string AdminPassword = "Integration.Admin-Only.6249!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("hospital_security_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Database:ApplyMigrations", "true");
        builder.UseSetting("Database:Seed", "false");
        builder.UseSetting("Authentication:BootstrapAdmin:UserName", AdminUserName);
        builder.UseSetting("Authentication:BootstrapAdmin:Password", AdminPassword);
        builder.UseSetting("Security:LoginRateLimit:PermitLimit", "1000");
    }

    public HttpClient CreateCookieClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
    });

    public Task InitializeAsync() => _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
