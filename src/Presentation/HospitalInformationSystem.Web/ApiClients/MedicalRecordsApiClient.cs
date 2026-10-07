using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using HospitalInformationSystem.Contracts.MedicalRecords;

namespace HospitalInformationSystem.Web.ApiClients;

public sealed class MedicalRecordsApiClient(HttpClient http)
{
    private const string AuthorHeader = "X-Medical-Worker-Id";


    public async Task<MedicalRecordDto?> FindForPatientAsync(Guid patientId,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/patients/{patientId}/medical-record", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        return await ApiResponseReader.ReadAsync<MedicalRecordDto>(response, cancellationToken);
    }

    public Task<MedicalRecordDto> CreateAsync(Guid patientId, Guid authorId,
        CancellationToken cancellationToken = default) =>
        SendAsync<MedicalRecordDto>(HttpMethod.Post, $"api/patients/{patientId}/medical-record", authorId, null,
            cancellationToken);

    public Task AddDiagnosisAsync(Guid recordId, Guid diagnosisId, Guid authorId,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"api/medical-records/{recordId}/diagnoses", authorId,
            new AddMedicalRecordDiagnosisRequest(diagnosisId), cancellationToken);

    public Task RemoveDiagnosisAsync(Guid recordId, Guid medicalRecordDiagnosisId, Guid authorId,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/medical-records/{recordId}/diagnoses/{medicalRecordDiagnosisId}", authorId,
            null, cancellationToken);

    public Task AddPrescriptionAsync(Guid recordId, string text, Guid authorId,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"api/medical-records/{recordId}/prescriptions", authorId,
            new SavePrescriptionRequest(text), cancellationToken);

    public Task CancelPrescriptionAsync(Guid recordId, Guid prescriptionId, Guid authorId,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"api/medical-records/{recordId}/prescriptions/{prescriptionId}/cancel", authorId,
            null, cancellationToken);

    public Task ReplacePrescriptionAsync(Guid recordId, Guid prescriptionId, string text, Guid authorId,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"api/medical-records/{recordId}/prescriptions/{prescriptionId}", authorId,
            new SavePrescriptionRequest(text), cancellationToken);

    public async Task<IReadOnlyList<MedicalRecordHistoryEventDto>> GetHistoryAsync(Guid recordId,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/medical-records/{recordId}/history", cancellationToken);
        return await ApiResponseReader.ReadListAsync<MedicalRecordHistoryEventDto>(response, cancellationToken);
    }

    public async Task<MedicalRecordSnapshotDto> GetSnapshotAsync(Guid recordId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var query = Uri.EscapeDataString(at.ToString("o", CultureInfo.InvariantCulture));
        using var response = await http.GetAsync($"api/medical-records/{recordId}/snapshot?at={query}",
            cancellationToken);
        return await ApiResponseReader.ReadAsync<MedicalRecordSnapshotDto>(response, cancellationToken);
    }

    private async Task SendAsync(HttpMethod method, string uri, Guid authorId, object? body,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(method, uri, authorId, body);
        using var response = await http.SendAsync(request, cancellationToken);
        await ApiResponseReader.EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string uri, Guid authorId, object? body,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(method, uri, authorId, body);
        using var response = await http.SendAsync(request, cancellationToken);
        return await ApiResponseReader.ReadAsync<T>(response, cancellationToken);
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string uri, Guid authorId, object? body)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(AuthorHeader, authorId.ToString());
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }
}
