using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Web.ApiClients;
using HospitalInformationSystem.Web.Authentication;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class BrowserSessionHandlerTests
{
    [Fact]
    public async Task Send_UnsafeRequest_ShouldIncludeCredentialsAndReuseAntiforgeryToken()
    {
        var requests = new List<(string Path, string? Token, object? Credentials)>();
        var state = new SessionState();
        var tokens = new AntiforgeryTokenStore();
        using var http = CreateClient(state, tokens, (request, _) =>
        {
            request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>(
                "WebAssemblyFetchOptions"), out var options);
            object? credentials = null;
            options?.TryGetValue("credentials", out credentials);
            requests.Add((request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues("X-CSRF-TOKEN", out var values) ? values.Single() : null,
                credentials));
            return Task.FromResult(request.RequestUri.AbsolutePath == "/api/auth/csrf"
                ? Json(new AntiforgeryTokenDto("request-token"))
                : new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        using var first = await http.PostAsync("api/diagnoses", null);
        using var second = await http.DeleteAsync("api/diagnoses/1");
        using var read = await http.GetAsync("api/diagnoses");

        requests.Should().Equal(
            ("/api/auth/csrf", null, "include"),
            ("/api/diagnoses", "request-token", "include"),
            ("/api/diagnoses/1", "request-token", "include"),
            ("/api/diagnoses", null, "include"));
    }

    [Fact]
    public async Task Send_ConcurrentWrites_ShouldShareAntiforgeryCookieRequest()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var csrfRequests = 0;
        using var http = CreateClient(new SessionState(), new AntiforgeryTokenStore(), async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/csrf")
            {
                Interlocked.Increment(ref csrfRequests);
                entered.TrySetResult();
                await release.Task;
                return Json(new AntiforgeryTokenDto("request-token"));
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var first = http.PostAsync("api/first", null);
        await entered.Task;
        var second = http.PostAsync("api/second", null);
        release.SetResult();
        using var firstResponse = await first;
        using var secondResponse = await second;

        csrfRequests.Should().Be(1);
    }

    [Fact]
    public async Task Send_TokenInvalidatedDuringLoad_ShouldNotCacheOrUseOldToken()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new AntiforgeryTokenStore();
        var csrfRequests = 0;
        string? submittedToken = null;
        using var http = CreateClient(new SessionState(), tokens, async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/csrf")
            {
                if (Interlocked.Increment(ref csrfRequests) == 1)
                {
                    entered.SetResult();
                    return await staleResponse.Task;
                }

                return Json(new AntiforgeryTokenDto("fresh-token"));
            }

            submittedToken = request.Headers.GetValues("X-CSRF-TOKEN").Single();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var pending = http.PostAsync("api/patients", null);
        await entered.Task;
        tokens.Reset();
        staleResponse.SetResult(Json(new AntiforgeryTokenDto("stale-token")));
        using var response = await pending;

        csrfRequests.Should().Be(2);
        submittedToken.Should().Be("fresh-token");
        tokens.RequestToken.Should().Be("fresh-token");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    public async Task Send_ResponseStatus_ShouldEndSessionOnlyWhenUnauthorized(HttpStatusCode status, bool expires)
    {
        var state = new SessionState();
        var account = new SessionDto(Guid.NewGuid(), "doctor", ApplicationRoles.MedicalWorker, Guid.NewGuid());
        state.SetSession(account);
        var tokens = new AntiforgeryTokenStore { RequestToken = "token" };
        using var http = CreateClient(state, tokens,
            (_, _) => Task.FromResult(new HttpResponseMessage(status)));

        using var response = await http.GetAsync("api/patients");

        state.Current.Should().Be(expires ? null : account);
        tokens.RequestToken.Should().Be(expires ? null : "token");
    }

    [Fact]
    public async Task Send_LateUnauthorizedResponse_ShouldPreserveNewSession()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = new SessionState();
        state.SetSession(new SessionDto(Guid.NewGuid(), "old", ApplicationRoles.MedicalWorker, Guid.NewGuid()));
        using var http = CreateClient(state, new AntiforgeryTokenStore(), (_, _) => response.Task);
        var pending = http.GetAsync("api/patients");
        var current = new SessionDto(Guid.NewGuid(), "new", ApplicationRoles.MedicalWorker, Guid.NewGuid());
        state.SetSession(current);

        response.SetResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var result = await pending;

        state.Current.Should().Be(current);
    }

    [Fact]
    public async Task LoginAndLogout_IdentityChange_ShouldInvalidatePreviousAntiforgeryTokens()
    {
        var current = new SessionDto(Guid.NewGuid(), "doctor", ApplicationRoles.MedicalWorker, Guid.NewGuid());
        var state = new SessionState();
        var tokens = new AntiforgeryTokenStore();
        var csrfRequests = 0;
        using var http = CreateClient(state, tokens, (request, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/api/auth/csrf" => Json(new AntiforgeryTokenDto($"token-{++csrfRequests}")),
            "/api/auth/login" => Json(current),
            _ => new HttpResponseMessage(HttpStatusCode.NoContent)
        }));
        var api = new SessionApiClient(http, state, tokens);

        await api.LoginAsync(new LoginRequest("doctor", "Doctor-Test-2026!"), CancellationToken.None);

        state.Current.Should().Be(current);
        tokens.RequestToken.Should().BeNull();

        await api.LogoutAsync(CancellationToken.None);

        csrfRequests.Should().Be(2);
        state.Current.Should().BeNull();
        tokens.RequestToken.Should().BeNull();
    }

    [Fact]
    public async Task GetAuthenticationState_ServerSession_ShouldIncludeRoleAndWorker()
    {
        var state = new SessionState();
        var workerId = Guid.NewGuid();
        state.SetSession(new SessionDto(Guid.NewGuid(), "doctor", ApplicationRoles.MedicalWorker, workerId));
        using var http = CreateClient(state, new AntiforgeryTokenStore(),
            (_, _) => throw new InvalidOperationException("An initialized session must not be loaded again."));
        using var provider = new SessionAuthenticationStateProvider(http, state);

        var authentication = await provider.GetAuthenticationStateAsync();

        authentication.User.Identity!.IsAuthenticated.Should().BeTrue();
        authentication.User.IsInRole(ApplicationRoles.MedicalWorker).Should().BeTrue();
        authentication.User.FindFirst(AuthClaimTypes.MedicalWorkerId)?.Value.Should().Be(workerId.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    public async Task InitializeSession_LateResponse_ShouldPreserveNewLoginOrLogout(
        HttpStatusCode status, bool loggedOut)
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = new SessionState();
        using var http = CreateClient(state, new AntiforgeryTokenStore(), (_, _) => response.Task);
        using var provider = new SessionAuthenticationStateProvider(http, state);
        var pending = provider.GetAuthenticationStateAsync();
        var current = loggedOut ? null
            : new SessionDto(Guid.NewGuid(), "new-doctor", ApplicationRoles.MedicalWorker, Guid.NewGuid());
        state.SetSession(current);
        var version = state.Version;
        response.SetResult(status == HttpStatusCode.OK
            ? Json(new SessionDto(Guid.NewGuid(), "old-admin", ApplicationRoles.Administrator, null))
            : new HttpResponseMessage(status));

        var authentication = await pending;

        state.Current.Should().Be(current);
        state.Version.Should().Be(version);
        authentication.User.Identity!.IsAuthenticated.Should().Be(!loggedOut);
        authentication.User.Identity.Name.Should().Be(current?.UserName);
    }

    private static HttpClient CreateClient(SessionState state, AntiforgeryTokenStore tokens,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new BrowserSessionHandler(state, tokens) { InnerHandler = new ResponseHandler(send) })
        {
            BaseAddress = new Uri("http://localhost:5200/")
        };

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value)
    };

    private sealed class ResponseHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
