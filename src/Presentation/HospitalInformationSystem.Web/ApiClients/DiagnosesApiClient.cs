using HospitalInformationSystem.Contracts.Diagnoses;

namespace HospitalInformationSystem.Web.ApiClients;

public sealed class DiagnosesApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<DiagnosisDefinitionDto>> ListAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("api/diagnoses", cancellationToken);
        return await ApiResponseReader.ReadListAsync<DiagnosisDefinitionDto>(response, cancellationToken);
    }

    public Task<DiagnosisDefinitionDto> CreateAsync(SaveDiagnosisRequest request,
        CancellationToken cancellationToken) =>
        http.SendJsonAsync<DiagnosisDefinitionDto>(HttpMethod.Post, "api/diagnoses", request, null, cancellationToken);

    public Task<DiagnosisDefinitionDto> UpdateAsync(Guid id, SaveDiagnosisRequest request,
        CancellationToken cancellationToken) =>
        http.SendJsonAsync<DiagnosisDefinitionDto>(HttpMethod.Put, $"api/diagnoses/{id}", request, null,
            cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        http.SendJsonAsync(HttpMethod.Delete, $"api/diagnoses/{id}", null, null, cancellationToken);

    public async Task<DiagnosisImportResultDto> ImportAsync(Stream file, string fileName,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(file), "file", fileName);
        using var response = await http.PostAsync("api/diagnoses/import", content, cancellationToken);
        return await ApiResponseReader.ReadAsync<DiagnosisImportResultDto>(response, cancellationToken);
    }
}
