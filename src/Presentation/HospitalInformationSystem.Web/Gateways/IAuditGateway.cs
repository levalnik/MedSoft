using HospitalInformationSystem.Contracts.Lab2;

namespace HospitalInformationSystem.Web.Gateways;

public interface IAuditGateway
{
    Task<PagedResult<AuditEventDto>> ListAsync(AuditQuery query, CancellationToken cancellationToken = default);
}
