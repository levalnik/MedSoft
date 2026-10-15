using System.Net;
using System.Security.Claims;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Web.ApiClients;
using Microsoft.AspNetCore.Components.Authorization;

namespace HospitalInformationSystem.Web.Authentication;

public sealed class SessionAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly SessionState _session;
    private Task<AuthenticationState>? _initialization;

    public SessionAuthenticationStateProvider(HttpClient http, SessionState session)
    {
        _http = http;
        _session = session;
        _session.Changed += OnSessionChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _session.Initialized
        ? Task.FromResult(BuildState())
        : _initialization ??= InitializeAsync();

    private async Task<AuthenticationState> InitializeAsync()
    {
        var version = _session.Version;
        try
        {
            using var response = await _http.GetAsync("api/auth/session");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (_session.Version == version)
                {
                    _session.SetSession(null);
                }
            }
            else
            {
                var session = await ApiResponseReader.ReadAsync<SessionDto>(response, CancellationToken.None);
                if (_session.Version == version)
                {
                    _session.SetSession(session);
                }
            }
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            if (_session.Version == version)
            {
                _session.SetSession(null);
            }
        }

        return BuildState();
    }

    private AuthenticationState BuildState()
    {
        if (_session.Current is not { } session)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.Id.ToString()),
            new(ClaimTypes.Name, session.UserName),
            new(ClaimTypes.Role, session.Role)
        };
        if (session.MedicalWorkerId is { } workerId)
        {
            claims.Add(new Claim(AuthClaimTypes.MedicalWorkerId, workerId.ToString()));
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookie")));
    }

    private void OnSessionChanged() => NotifyAuthenticationStateChanged(Task.FromResult(BuildState()));

    public void Dispose() => _session.Changed -= OnSessionChanged;
}
