using System.Net.Http.Json;
using HospitalInformationSystem.Contracts.MedicalWorkers;

namespace HospitalInformationSystem.Web.ApiClients;

public sealed class MedicalWorkersApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<MedicalWorkerDto>> ListAsync(bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        using var response =
            await http.GetAsync($"api/medical-workers?includeDeleted={includeDeleted}", cancellationToken);
        return await ApiResponseReader.ReadListAsync<MedicalWorkerDto>(response, cancellationToken);
    }

    public async Task<MedicalWorkerDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/medical-workers/{id}", cancellationToken);
        return await ApiResponseReader.ReadAsync<MedicalWorkerDto>(response, cancellationToken);
    }

    public async Task<MedicalWorkerDto> SaveAsync(Guid? id, SaveMedicalWorkerRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = id is null
            ? await http.PostAsJsonAsync("api/medical-workers", request, cancellationToken)
            : await http.PutAsJsonAsync($"api/medical-workers/{id}", request, cancellationToken);
        return await ApiResponseReader.ReadAsync<MedicalWorkerDto>(response, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/medical-workers/{id}", cancellationToken);
        await ApiResponseReader.EnsureSuccessAsync(response, cancellationToken);
    }
}
