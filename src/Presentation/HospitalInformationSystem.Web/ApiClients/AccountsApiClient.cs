using HospitalInformationSystem.Contracts.Auth;

namespace HospitalInformationSystem.Web.ApiClients;

public sealed class AccountsApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("api/accounts", cancellationToken);
        return await ApiResponseReader.ReadListAsync<AccountDto>(response, cancellationToken);
    }

    public Task<AccountDto> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken) =>
        http.SendJsonAsync<AccountDto>(HttpMethod.Post, "api/accounts", request, null, cancellationToken);
}
