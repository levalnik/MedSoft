using HospitalInformationSystem.Web;
using HospitalInformationSystem.Web.ApiClients;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]
                          ?? throw new InvalidOperationException("ApiBaseUrl is required.")),
    Timeout = TimeSpan.FromSeconds(20)
});
builder.Services.AddScoped<MedicalWorkersApiClient>();
builder.Services.AddScoped<DiagnosesApiClient>();
await builder.Build().RunAsync();
