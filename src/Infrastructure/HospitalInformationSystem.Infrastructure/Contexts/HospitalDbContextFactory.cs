using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HospitalInformationSystem.Infrastructure.Contexts;

public sealed class HospitalDbContextFactory : IDesignTimeDbContextFactory<HospitalDbContext>
{
    public HospitalDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                               ?? "Host=localhost;Database=hospital;Username=postgres";
        return new HospitalDbContext(new DbContextOptionsBuilder<HospitalDbContext>()
            .UseNpgsql(connectionString).Options);
    }
}
