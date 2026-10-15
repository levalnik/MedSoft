using Microsoft.AspNetCore.Identity;

namespace HospitalInformationSystem.Infrastructure.Identity;

public sealed class HospitalUser : IdentityUser<Guid>
{
    public Guid? MedicalWorkerId { get; set; }
}
