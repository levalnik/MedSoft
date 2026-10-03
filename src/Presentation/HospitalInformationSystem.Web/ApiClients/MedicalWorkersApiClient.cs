using System.Net.Http.Json;
using System.Text.Json;
using HospitalInformationSystem.Contracts.MedicalWorkers;

namespace HospitalInformationSystem.Web.ApiClients;

public sealed class MedicalWorkersApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<MedicalWorkerDto>> ListAsync(bool includeDeleted, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/medical-workers?includeDeleted={includeDeleted}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MedicalWorkerDto[]>(cancellationToken) ?? [];
    }

    public async Task<MedicalWorkerDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/medical-workers/{id}", cancellationToken);
        return await ReadWorkerAsync(response, cancellationToken);
    }

    public async Task<MedicalWorkerDto> SaveAsync(Guid? id, SaveMedicalWorkerRequest request, CancellationToken cancellationToken = default)
    {
        using var response = id is null
            ? await http.PostAsJsonAsync("api/medical-workers", request, cancellationToken)
            : await http.PutAsJsonAsync($"api/medical-workers/{id}", request, cancellationToken);
        return await ReadWorkerAsync(response, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/medical-workers/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task<MedicalWorkerDto> ReadWorkerAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MedicalWorkerDto>(cancellationToken)
            ?? throw new ApiException("Сервер вернул пустой ответ.");
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
