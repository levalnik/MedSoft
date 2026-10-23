using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Lab2;

namespace HospitalInformationSystem.Web.Gateways;

public interface IAuthenticationGateway
{
    Task<SetupStatusDto> GetSetupStatusAsync(CancellationToken cancellationToken = default);
    Task<SetupChallengeDto> BeginSetupAsync(BeginSetupRequest request, string setupToken,
        CancellationToken cancellationToken = default);
    Task ConfirmSetupAsync(ConfirmSetupRequest request, string setupToken,
        CancellationToken cancellationToken = default);
    Task<AuthenticationSessionDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthenticationSessionDto?> GetSessionAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
