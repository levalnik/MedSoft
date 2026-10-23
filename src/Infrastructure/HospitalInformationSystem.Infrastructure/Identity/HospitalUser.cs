using Microsoft.AspNetCore.Identity;

namespace HospitalInformationSystem.Infrastructure.Identity;

public sealed class HospitalUser : IdentityUser<Guid>
{
    public Guid? MedicalWorkerId { get; set; }
    public bool IsAdministrator { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
