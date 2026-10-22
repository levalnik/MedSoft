using HospitalInformationSystem.Web;
using HospitalInformationSystem.Web.Authentication;
using HospitalInformationSystem.Web.Configuration;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddHospitalWebClients(builder.Configuration, builder.HostEnvironment.Environment);
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, SessionAuthenticationStateProvider>();
await builder.Build().RunAsync();
