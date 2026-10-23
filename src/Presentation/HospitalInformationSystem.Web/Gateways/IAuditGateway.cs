using HospitalInformationSystem.Contracts.Security;

namespace HospitalInformationSystem.Web.Gateways;

public interface IAuditGateway
{
    Task<PagedResult<AuditEventDto>> ListAsync(AuditQuery query, CancellationToken cancellationToken);
}
