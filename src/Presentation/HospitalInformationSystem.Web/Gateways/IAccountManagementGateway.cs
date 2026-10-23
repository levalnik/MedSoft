using HospitalInformationSystem.Contracts.Lab2;

namespace HospitalInformationSystem.Web.Gateways;

public interface IAccountManagementGateway
{
    Task<IReadOnlyList<Lab2AccountDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<CreateWorkerAccountResultDto> CreateForWorkerAsync(Guid medicalWorkerId,
        CreateWorkerAccountRequest request, CancellationToken cancellationToken = default);
    Task<ResetAccountPasswordResultDto> ResetPasswordAsync(Guid accountId,
        CancellationToken cancellationToken = default);
    Task<SetAccountActiveResultDto> SetActiveAsync(Guid accountId, bool isActive,
        CancellationToken cancellationToken = default);
}
