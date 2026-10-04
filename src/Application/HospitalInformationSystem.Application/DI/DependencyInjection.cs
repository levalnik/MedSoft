using FluentValidation;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.MedicalRecords;
using HospitalInformationSystem.Application.MedicalRecords.Snapshot;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalInformationSystem.Application.DI;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<MedicalRecordCommandContext>();
        services.AddScoped<IMedicalRecordSnapshotBuilder, MedicalRecordSnapshotBuilder>();
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
