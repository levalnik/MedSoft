using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalInformationSystem.Infrastructure.Contexts;

public sealed class HospitalDbContext(DbContextOptions<HospitalDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<MedicalWorkerDbo> MedicalWorkers => Set<MedicalWorkerDbo>();
    public DbSet<PatientDbo> Patients => Set<PatientDbo>();
    public DbSet<DiagnosisDefinitionDbo> DiagnosisDefinitions => Set<DiagnosisDefinitionDbo>();
    public DbSet<MedicalRecordDbo> MedicalRecords => Set<MedicalRecordDbo>();
    public DbSet<MedicalRecordDiagnosisDbo> MedicalRecordDiagnoses => Set<MedicalRecordDiagnosisDbo>();
    public DbSet<PrescriptionDbo> Prescriptions => Set<PrescriptionDbo>();
    public DbSet<MedicalRecordHistoryEventDbo> MedicalRecordHistoryEvents => Set<MedicalRecordHistoryEventDbo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HospitalDbContext).Assembly);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new BusinessConflictException(
                "Данные уже изменены другим запросом. Обновите страницу и повторите операцию.", exception);
        }
        catch (Exception exception) when (exception.GetBaseException() is PostgresException postgres &&
                                          postgres.SqlState is PostgresErrorCodes.UniqueViolation
                                              or PostgresErrorCodes.ForeignKeyViolation
                                              or PostgresErrorCodes.CheckViolation
                                              or PostgresErrorCodes.SerializationFailure
                                              or PostgresErrorCodes.DeadlockDetected)
        {
            throw new BusinessConflictException(
                "Сохранение конфликтует с существующей записью, связями или одновременным изменением. Обновите данные и повторите операцию.",
                exception);
        }
    }
}
