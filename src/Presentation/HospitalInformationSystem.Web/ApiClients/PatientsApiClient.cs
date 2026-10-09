using HospitalInformationSystem.Contracts.Patients;

namespace HospitalInformationSystem.Web.ApiClients;

public sealed class PatientsApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<PatientDto>> ListAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("api/patients", cancellationToken);
        return await ApiResponseReader.ReadListAsync<PatientDto>(response, cancellationToken);
    }

    public async Task<PatientDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"api/patients/{id}", cancellationToken);
        return await ApiResponseReader.ReadAsync<PatientDto>(response, cancellationToken);
    }
}
