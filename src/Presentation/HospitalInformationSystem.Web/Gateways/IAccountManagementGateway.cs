using HospitalInformationSystem.Contracts.Security;

namespace HospitalInformationSystem.Web.Gateways;

public interface IAccountManagementGateway
{
    Task<IReadOnlyList<AccountDetailsDto>> ListAsync(CancellationToken cancellationToken);
    Task<CreateWorkerAccountResultDto> CreateForWorkerAsync(Guid medicalWorkerId,
        CreateWorkerAccountRequest request, CancellationToken cancellationToken);
    Task<ResetAccountPasswordResultDto> ResetPasswordAsync(Guid accountId,
        CancellationToken cancellationToken);
    Task<SetAccountActiveResultDto> SetActiveAsync(Guid accountId, bool isActive,
        CancellationToken cancellationToken);
}
