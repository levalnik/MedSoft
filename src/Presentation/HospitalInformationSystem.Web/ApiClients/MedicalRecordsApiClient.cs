using System.Globalization;
using System.Net;
using HospitalInformationSystem.Contracts.MedicalRecords;

namespace HospitalInformationSystem.Web.ApiClients;

public sealed class MedicalRecordsApiClient(HttpClient http)
{
    private const string AuthorHeader = "X-Medical-Worker-Id";

    public async Task<MedicalRecordDto?> FindForPatientAsync(Guid patientId,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"api/patients/{patientId}/medical-record", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ApiResponseReader.ReadAsync<MedicalRecordDto>(response, cancellationToken);
    }

    public Task<MedicalRecordDto> CreateAsync(Guid patientId, Guid authorId,
        CancellationToken cancellationToken) =>
        http.SendJsonAsync<MedicalRecordDto>(HttpMethod.Post, $"api/patients/{patientId}/medical-record",
            null, AuthorHeaders(authorId), cancellationToken);

    public Task AddDiagnosisAsync(Guid recordId, Guid diagnosisId, Guid authorId,
        CancellationToken cancellationToken) =>
        http.SendJsonAsync(HttpMethod.Post, $"api/medical-records/{recordId}/diagnoses",
            new AddMedicalRecordDiagnosisRequest(diagnosisId), AuthorHeaders(authorId), cancellationToken);

    public Task RemoveDiagnosisAsync(Guid recordId, Guid medicalRecordDiagnosisId, Guid authorId,
        CancellationToken cancellationToken) =>
        http.SendJsonAsync(HttpMethod.Delete, $"api/medical-records/{recordId}/diagnoses/{medicalRecordDiagnosisId}",
            null, AuthorHeaders(authorId), cancellationToken);

    public Task AddPrescriptionAsync(Guid recordId, string text, Guid authorId,
        CancellationToken cancellationToken) =>
        http.SendJsonAsync(HttpMethod.Post, $"api/medical-records/{recordId}/prescriptions",
            new SavePrescriptionRequest(text), AuthorHeaders(authorId), cancellationToken);

    public Task CancelPrescriptionAsync(Guid recordId, Guid prescriptionId, Guid authorId,
        CancellationToken cancellationToken) =>
        http.SendJsonAsync(HttpMethod.Post, $"api/medical-records/{recordId}/prescriptions/{prescriptionId}/cancel",
            null, AuthorHeaders(authorId), cancellationToken);

    public Task ReplacePrescriptionAsync(Guid recordId, Guid prescriptionId, string text, Guid authorId,
        CancellationToken cancellationToken) =>
        http.SendJsonAsync(HttpMethod.Put, $"api/medical-records/{recordId}/prescriptions/{prescriptionId}",
            new SavePrescriptionRequest(text), AuthorHeaders(authorId), cancellationToken);

    public async Task<IReadOnlyList<MedicalRecordHistoryEventDto>> GetHistoryAsync(Guid recordId,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"api/medical-records/{recordId}/history", cancellationToken);
        return await ApiResponseReader.ReadListAsync<MedicalRecordHistoryEventDto>(response, cancellationToken);
    }

    public async Task<MedicalRecordSnapshotDto> GetSnapshotAsync(Guid recordId, DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString(at.ToString("o", CultureInfo.InvariantCulture));
        using var response = await http.GetAsync($"api/medical-records/{recordId}/snapshot?at={query}",
            cancellationToken);
        return await ApiResponseReader.ReadAsync<MedicalRecordSnapshotDto>(response, cancellationToken);
    }

    private static Dictionary<string, string> AuthorHeaders(Guid authorId) =>
        new() { [AuthorHeader] = authorId.ToString() };
}