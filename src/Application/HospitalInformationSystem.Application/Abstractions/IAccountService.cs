using HospitalInformationSystem.Contracts.Auth;

namespace HospitalInformationSystem.Application.Abstractions;

public interface IAccountService
{
    Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken cancellationToken);
    Task<AccountDto> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken);
    Task<SessionDto?> GetSessionAsync(Guid userId, CancellationToken cancellationToken);
}
