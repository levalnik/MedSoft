using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Authentication;
using HospitalInformationSystem.Web.Development;
using HospitalInformationSystem.Web.Gateways;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalInformationSystem.Web.Configuration;

public static class WebClientRegistration
{
    public static IServiceCollection AddHospitalWebClients(this IServiceCollection services,
        IConfiguration configuration, string environment)
    {
        var mock = UiMockOptions.FromConfiguration(configuration, environment);
        services.AddSingleton(mock);
        services.AddScoped<SessionState>();
        services.AddScoped<AntiforgeryTokenStore>();
        if (mock.Enabled)
        {
            services.AddScoped<UiMockState>();
            services.AddScoped<UiMockHttpMessageHandler>();
        }

        services.AddScoped(provider => new HttpClient(new BrowserSessionHandler(
            provider.GetRequiredService<SessionState>(), provider.GetRequiredService<AntiforgeryTokenStore>())
        {
            InnerHandler = mock.Enabled ? provider.GetRequiredService<UiMockHttpMessageHandler>() : new HttpClientHandler()
        })
        {
            BaseAddress = mock.Enabled ? new Uri("https://ui-mock.invalid/")
                : new Uri(configuration["ApiBaseUrl"]
                          ?? throw new InvalidOperationException("ApiBaseUrl is required.")),
            Timeout = TimeSpan.FromSeconds(20)
        });

        services.AddScoped<MedicalWorkersApiClient>();
        services.AddScoped<DiagnosesApiClient>();
        services.AddScoped<PatientsApiClient>();
        services.AddScoped<MedicalRecordsApiClient>();
        services.AddScoped<SessionApiClient>();
        services.AddScoped<AccountsApiClient>();
        services.AddScoped<IAuthenticationGateway, AuthenticationGateway>();
        services.AddScoped<IAccountManagementGateway, AccountManagementGateway>();
        services.AddScoped<IAuditGateway, AuditGateway>();
        services.AddScoped<IMedicalRecordsGateway, MedicalRecordsGateway>();
        return services;
    }
}
