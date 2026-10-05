using System.Net.Http.Json;
using System.Text.Json;

namespace HospitalInformationSystem.Web.ApiClients;

internal static class ApiResponseReader
{
    private const string InvalidResponseMessage =
        "Сервер вернул некорректный ответ. Обновите данные, чтобы проверить результат операции.";
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ResponseJsonOptions, cancellationToken)
                ?? throw new ApiException(InvalidResponseMessage);
        }
        catch (JsonException exception)
        {
            throw new ApiException(InvalidResponseMessage, exception);
        }
    }

    public static async Task<IReadOnlyList<T>> ReadListAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        var items = await ReadAsync<T[]>(response, cancellationToken);
        if (items.Any(item => item is null)) throw new ApiException(InvalidResponseMessage);
        return items;
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        ProblemResponse? problem = null;
        try { problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(cancellationToken); }
        catch (JsonException) { }

        var errors = problem?.Errors?.Values
            .SelectMany(values => values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .ToArray();

        var message = errors is { Length: > 0 } ? string.Join(" ", errors)
            : !string.IsNullOrWhiteSpace(problem?.Detail) ? problem.Detail
            : !string.IsNullOrWhiteSpace(problem?.Title) ? problem.Title
            : $"Ошибка сервера: {(int)response.StatusCode}.";
        throw new ApiException(message);
    }

    private sealed record ProblemResponse(string? Title, string? Detail, Dictionary<string, string[]?>? Errors);
}
