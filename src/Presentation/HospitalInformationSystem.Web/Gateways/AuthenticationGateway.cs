using System.Net;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Security;
using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Authentication;

namespace HospitalInformationSystem.Web.Gateways;

public sealed class AuthenticationGateway(HttpClient http, SessionApiClient client, SessionState session)
    : IAuthenticationGateway
{
    public Task<SetupStatusDto> GetSetupStatusAsync(CancellationToken cancellationToken) =>
        GatewayHttpTransport.SendAsync<SetupStatusDto>(http, HttpMethod.Get, "api/auth/setup/status", null,
            cancellationToken);

    public Task<SetupChallengeDto> BeginSetupAsync(BeginSetupRequest request, string setupToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setupToken);
        return GatewayHttpTransport.SendAsync<SetupChallengeDto>(http, HttpMethod.Post, "api/auth/setup/begin", request,
            cancellationToken, setupToken);
    }

    public Task ConfirmSetupAsync(ConfirmSetupRequest request, string setupToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setupToken);
        return GatewayHttpTransport.SendAsync(http, HttpMethod.Post, "api/auth/setup/confirm", request,
            cancellationToken, setupToken);
    }

    public async Task<AuthenticationSessionDto> LoginAsync(LoginRequest request,
        CancellationToken cancellationToken)
    {
        await client.LoginAsync(request, cancellationToken);
        return session.Current is { } current ? ToContract(current)
            : throw new ApiException("Сервер не вернул действующую сессию.");
    }

    public async Task<AuthenticationSessionDto?> GetSessionAsync(CancellationToken cancellationToken)
    {
        var version = session.Version;
        using var response = await http.GetAsync("api/auth/session", cancellationToken);
        var current = response.StatusCode == HttpStatusCode.Unauthorized ? null
            : await ApiResponseReader.ReadAsync<SessionDto>(response, cancellationToken);
        if (session.Version == version) session.SetSession(current);
        return session.Current is { } active ? ToContract(active) : null;
    }

    public Task LogoutAsync(CancellationToken cancellationToken) =>
        client.LogoutAsync(cancellationToken);

    private static AuthenticationSessionDto ToContract(SessionDto current) =>
        new(current.Id, current.UserName, current.Role, current.MedicalWorkerId, current.MedicalWorkerRole);
}
