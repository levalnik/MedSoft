using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Web.Authentication;

namespace HospitalInformationSystem.Web.ApiClients;

public sealed class SessionApiClient(HttpClient http, SessionState session, AntiforgeryTokenStore tokens)
{
    public async Task LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var current = await http.SendJsonAsync<SessionDto>(HttpMethod.Post, "api/auth/login", request, null,
            cancellationToken);
        tokens.Reset();
        session.SetSession(current);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        await http.SendJsonAsync(HttpMethod.Post, "api/auth/logout", null, null, cancellationToken);
        tokens.Reset();
        session.SetSession(null);
    }
}
