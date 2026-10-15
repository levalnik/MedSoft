using System.Net;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Web.ApiClients;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace HospitalInformationSystem.Web.Authentication;

public sealed class BrowserSessionHandler(SessionState session, AntiforgeryTokenStore tokens) : DelegatingHandler
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var sessionVersion = session.Version;
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        if (RequiresAntiforgery(request.Method))
        {
            await _tokenLock.WaitAsync(cancellationToken);
            try
            {
                var token = tokens.RequestToken;
                while (token is null)
                {
                    var tokenVersion = tokens.Version;
                    using var tokenRequest = new HttpRequestMessage(HttpMethod.Get,
                        new Uri(request.RequestUri!, "/api/auth/csrf"));
                    tokenRequest.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
                    using var tokenResponse = await base.SendAsync(tokenRequest, cancellationToken);
                    var result = await ApiResponseReader.ReadAsync<AntiforgeryTokenDto>(tokenResponse,
                        cancellationToken);
                    if (tokens.Version == tokenVersion)
                    {
                        token = result.RequestToken;
                        tokens.RequestToken = token;
                    }
                }

                request.Headers.Add("X-CSRF-TOKEN", token);
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized && session.Version == sessionVersion)
        {
            tokens.Reset();
            session.SetSession(null);
        }

        return response;
    }

    private static bool RequiresAntiforgery(HttpMethod method) =>
        method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Options;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tokenLock.Dispose();
        }

        base.Dispose(disposing);
    }
}
