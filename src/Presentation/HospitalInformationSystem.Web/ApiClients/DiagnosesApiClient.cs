using System.Net.Http.Json;
using System.Text.Json;
using HospitalInformationSystem.Contracts.Diagnoses;

namespace HospitalInformationSystem.Web.ApiClients;

public sealed class DiagnosesApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<DiagnosisDefinitionDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/diagnoses", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DiagnosisDefinitionDto[]>(cancellationToken) ?? [];
    }

    public async Task<DiagnosisImportResultDto> ImportAsync(Stream file, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(file), "file", fileName);
        using var response = await http.PostAsync("api/diagnoses/import", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DiagnosisImportResultDto>(cancellationToken)
            ?? throw new ApiException("Сервер вернул пустой ответ. Обновите список, чтобы проверить результат импорта.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        ProblemResponse? problem = null;
        try { problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(cancellationToken); }
        catch (JsonException) { /* A proxy may return a non-JSON error. */ }
        var errors = problem?.Errors?.Values.SelectMany(x => x).Distinct().ToArray();
        throw new ApiException(errors is { Length: > 0 }
            ? string.Join(" ", errors)
            : problem?.Detail ?? problem?.Title ?? $"Ошибка сервера: {(int)response.StatusCode}.");
    }

    private sealed record ProblemResponse(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
