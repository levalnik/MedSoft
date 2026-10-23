using System.Net;
using System.Net.Http.Json;
using HospitalInformationSystem.Web.ApiClients;

namespace HospitalInformationSystem.Web.Gateways;

internal static class GatewayHttpTransport
{
    public static async Task<T> SendAsync<T>(HttpClient http, HttpMethod method, string uri, object? body,
        CancellationToken cancellationToken, string? setupToken = null)
    {
        using var request = CreateRequest(method, uri, body, setupToken);
        using var response = await http.SendAsync(request, cancellationToken);
        EnsureSupported(response);
        return await ApiResponseReader.ReadAsync<T>(response, cancellationToken);
    }

    public static async Task SendAsync(HttpClient http, HttpMethod method, string uri, object? body,
        CancellationToken cancellationToken, string? setupToken = null)
    {
        using var request = CreateRequest(method, uri, body, setupToken);
        using var response = await http.SendAsync(request, cancellationToken);
        EnsureSupported(response);
        await ApiResponseReader.EnsureSuccessAsync(response, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, object? body, string? setupToken)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (setupToken is not null) request.Headers.Add("X-Setup-Token", setupToken);
        return request;
    }

    private static void EnsureSupported(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NotImplemented)
        {
            throw new ApiException("Сервер пока не поддерживает эту операцию или запрошенная запись не найдена.",
                response.StatusCode);
        }
    }
}
