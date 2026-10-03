using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalInformationSystem.Infrastructure.Persistence;

public sealed class HospitalDbContext(DbContextOptions<HospitalDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<MedicalWorkerDbo> MedicalWorkers => Set<MedicalWorkerDbo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HospitalDbContext).Assembly);
}
