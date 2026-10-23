using HospitalInformationSystem.Contracts.Security;

namespace HospitalInformationSystem.Web.Gateways;

public sealed class AccountManagementGateway(HttpClient http) : IAccountManagementGateway
{
    public async Task<IReadOnlyList<AccountDetailsDto>> ListAsync(CancellationToken cancellationToken)
    {
        var accounts = await GatewayHttpTransport.SendAsync<AccountDetailsDto[]>(http, HttpMethod.Get, "api/accounts", null,
            cancellationToken);
        if (accounts.Any(account => account is null))
            throw new ApiClients.ApiException("Сервер вернул некорректный список учётных записей.");
        return accounts;
    }

    public Task<CreateWorkerAccountResultDto> CreateForWorkerAsync(Guid medicalWorkerId,
        CreateWorkerAccountRequest request, CancellationToken cancellationToken) =>
        GatewayHttpTransport.SendAsync<CreateWorkerAccountResultDto>(http, HttpMethod.Post,
            $"api/medical-workers/{medicalWorkerId}/account", request, cancellationToken);

    public Task<ResetAccountPasswordResultDto> ResetPasswordAsync(Guid accountId,
        CancellationToken cancellationToken) =>
        GatewayHttpTransport.SendAsync<ResetAccountPasswordResultDto>(http, HttpMethod.Post,
            $"api/accounts/{accountId}/reset-password", null, cancellationToken);

    public Task<SetAccountActiveResultDto> SetActiveAsync(Guid accountId, bool isActive,
        CancellationToken cancellationToken) =>
        GatewayHttpTransport.SendAsync<SetAccountActiveResultDto>(http, HttpMethod.Put,
            $"api/accounts/{accountId}/activity", new SetAccountActiveRequest(isActive), cancellationToken);
}
