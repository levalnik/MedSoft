using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Domain.MedicalWorkers;
using Microsoft.EntityFrameworkCore;

namespace HospitalInformationSystem.Infrastructure.Persistence;

public sealed class HospitalDbContext(DbContextOptions<HospitalDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<MedicalWorker> MedicalWorkers => Set<MedicalWorker>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HospitalDbContext).Assembly);
}
