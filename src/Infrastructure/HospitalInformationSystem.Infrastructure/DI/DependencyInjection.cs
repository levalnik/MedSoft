using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Infrastructure.Persistence;
using HospitalInformationSystem.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalInformationSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<HospitalDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<HospitalDbContext>());
        services.AddScoped<IMedicalWorkerRepository, MedicalWorkerRepository>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
