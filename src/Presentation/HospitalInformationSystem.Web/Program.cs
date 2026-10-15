using HospitalInformationSystem.Web;
using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped<SessionState>();
builder.Services.AddScoped<AntiforgeryTokenStore>();
builder.Services.AddScoped(services => new HttpClient(new BrowserSessionHandler(
    services.GetRequiredService<SessionState>(), services.GetRequiredService<AntiforgeryTokenStore>())
{
    InnerHandler = new HttpClientHandler()
})
{
    BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]
                          ?? throw new InvalidOperationException("ApiBaseUrl is required.")),
    Timeout = TimeSpan.FromSeconds(20)
});
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, SessionAuthenticationStateProvider>();
builder.Services.AddScoped<MedicalWorkersApiClient>();
builder.Services.AddScoped<DiagnosesApiClient>();
builder.Services.AddScoped<PatientsApiClient>();
builder.Services.AddScoped<MedicalRecordsApiClient>();
builder.Services.AddScoped<SessionApiClient>();
builder.Services.AddScoped<AccountsApiClient>();
await builder.Build().RunAsync();
