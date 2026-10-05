using HospitalInformationSystem.Contracts.Diagnoses;

namespace HospitalInformationSystem.Web.ApiClients;

public sealed class DiagnosesApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<DiagnosisDefinitionDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/diagnoses", cancellationToken);
        return await ApiResponseReader.ReadListAsync<DiagnosisDefinitionDto>(response, cancellationToken);
    }

    public async Task<DiagnosisImportResultDto> ImportAsync(Stream file, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(file), "file", fileName);
        using var response = await http.PostAsync("api/diagnoses/import", content, cancellationToken);
        return await ApiResponseReader.ReadAsync<DiagnosisImportResultDto>(response, cancellationToken);
    }
}
