using System.Net.Http.Json;

namespace HospitalInformationSystem.Web.ApiClients;

internal static class HttpClientExtensions
{
    public static async Task SendJsonAsync(
        this HttpClient http,
        HttpMethod method,
        string uri,
        object? body,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, body, headers);
        using var response = await http.SendAsync(request, cancellationToken);
        await ApiResponseReader.EnsureSuccessAsync(response, cancellationToken);
    }

    public static async Task<T> SendJsonAsync<T>(
        this HttpClient http,
        HttpMethod method,
        string uri,
        object? body,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, body, headers);
        using var response = await http.SendAsync(request, cancellationToken);
        return await ApiResponseReader.ReadAsync<T>(response, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string uri,
        object? body,
        IReadOnlyDictionary<string, string>? headers)
    {
        var request = new HttpRequestMessage(method, uri);

        if (headers is not null)
        {
            foreach (var (name, value) in headers)
            {
                request.Headers.Add(name, value);
            }
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}