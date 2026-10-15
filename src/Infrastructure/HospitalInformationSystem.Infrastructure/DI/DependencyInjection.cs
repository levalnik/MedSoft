using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Initializers;
using HospitalInformationSystem.Infrastructure.Identity;
using HospitalInformationSystem.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalInformationSystem.Infrastructure.DI;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<HospitalDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentityCore<HospitalUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<HospitalDbContext>()
            .AddSignInManager()
            .AddPasswordValidator<PasswordLengthValidator>();
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 220_000);
        services.AddScoped<AccountService>();
        services.AddScoped<IAccountService>(provider => provider.GetRequiredService<AccountService>());
        services.AddScoped<IdentityBootstrapper>();
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
