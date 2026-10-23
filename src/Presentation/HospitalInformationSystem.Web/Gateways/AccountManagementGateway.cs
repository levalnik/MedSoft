using HospitalInformationSystem.Contracts.Lab2;

namespace HospitalInformationSystem.Web.Gateways;

public sealed class AccountManagementGateway(HttpClient http) : IAccountManagementGateway
{
    public async Task<IReadOnlyList<Lab2AccountDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await Lab2ApiTransport.SendAsync<Lab2AccountDto[]>(http, HttpMethod.Get, "api/accounts", null,
            cancellationToken);
        if (accounts.Any(account => account is null))
            throw new ApiClients.ApiException("Сервер вернул некорректный список учётных записей.");
        return accounts;
    }

    public Task<CreateWorkerAccountResultDto> CreateForWorkerAsync(Guid medicalWorkerId,
        CreateWorkerAccountRequest request, CancellationToken cancellationToken = default) =>
        Lab2ApiTransport.SendAsync<CreateWorkerAccountResultDto>(http, HttpMethod.Post,
            $"api/medical-workers/{medicalWorkerId}/account", request, cancellationToken);

    public Task<ResetAccountPasswordResultDto> ResetPasswordAsync(Guid accountId,
        CancellationToken cancellationToken = default) =>
        Lab2ApiTransport.SendAsync<ResetAccountPasswordResultDto>(http, HttpMethod.Post,
            $"api/accounts/{accountId}/reset-password", null, cancellationToken);

    public Task<SetAccountActiveResultDto> SetActiveAsync(Guid accountId, bool isActive,
        CancellationToken cancellationToken = default) =>
        Lab2ApiTransport.SendAsync<SetAccountActiveResultDto>(http, HttpMethod.Put,
            $"api/accounts/{accountId}/activity", new SetAccountActiveRequest(isActive), cancellationToken);
}
