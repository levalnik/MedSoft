using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Initializers;
using HospitalInformationSystem.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalInformationSystem.Infrastructure.DI;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<HospitalDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<HospitalDbContext>());
        services.AddScoped<IMedicalWorkerRepository, MedicalWorkerRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IDiagnosisRepository, DiagnosisRepository>();
        services.AddScoped<MedicalRecordRepository>();
        services.AddScoped<IMedicalRecordRepository>(provider => provider.GetRequiredService<MedicalRecordRepository>());
        services.AddScoped<IMedicalRecordPresence>(provider => provider.GetRequiredService<MedicalRecordRepository>());
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
