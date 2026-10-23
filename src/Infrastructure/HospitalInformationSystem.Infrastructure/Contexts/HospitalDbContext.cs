using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using HospitalInformationSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalInformationSystem.Infrastructure.Contexts;

public sealed class HospitalDbContext(DbContextOptions<HospitalDbContext> options, TimeProvider? timeProvider = null)
    : IdentityDbContext<HospitalUser, IdentityRole<Guid>, Guid>(options), IUnitOfWork
{
    public DbSet<MedicalWorkerDbo> MedicalWorkers => Set<MedicalWorkerDbo>();
    public DbSet<PatientDbo> Patients => Set<PatientDbo>();
    public DbSet<DiagnosisDefinitionDbo> DiagnosisDefinitions => Set<DiagnosisDefinitionDbo>();
    public DbSet<MedicalRecordDbo> MedicalRecords => Set<MedicalRecordDbo>();
    public DbSet<MedicalRecordDiagnosisDbo> MedicalRecordDiagnoses => Set<MedicalRecordDiagnosisDbo>();
    public DbSet<PrescriptionDbo> Prescriptions => Set<PrescriptionDbo>();
    public DbSet<MedicalRecordHistoryEventDbo> MedicalRecordHistoryEvents => Set<MedicalRecordHistoryEventDbo>();
    public DbSet<RefreshSessionDbo> RefreshSessions => Set<RefreshSessionDbo>();
    public DbSet<AuditEventDbo> AuditEvents => Set<AuditEventDbo>();
    public DbSet<AdministratorSetupStateDbo> AdministratorSetupStates => Set<AdministratorSetupStateDbo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HospitalDbContext).Assembly);
        modelBuilder.Entity<HospitalUser>(user =>
        {
            user.HasIndex(x => x.MedicalWorkerId).IsUnique();
            user.HasOne<MedicalWorkerDbo>().WithMany().HasForeignKey(x => x.MedicalWorkerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RejectHistoryChanges();
        UpdateAccountTimestamps();
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new BusinessConflictException(
                "Данные уже изменены другим запросом. Обновите страницу и повторите операцию.", exception);
        }
        catch (Exception exception) when (IsDatabaseConflict(exception))
        {
            throw new BusinessConflictException(
                "Сохранение конфликтует с существующей записью, связями или одновременным изменением. Обновите данные и повторите операцию.",
                exception);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectHistoryChanges();
        UpdateAccountTimestamps();
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new BusinessConflictException(
                "Данные уже изменены другим запросом. Обновите страницу и повторите операцию.", exception);
        }
        catch (Exception exception) when (IsDatabaseConflict(exception))
        {
            throw new BusinessConflictException(
                "Сохранение конфликтует с существующей записью, связями или одновременным изменением. Обновите данные и повторите операцию.",
                exception);
        }
    }

    private static bool IsDatabaseConflict(Exception exception) =>
        exception.GetBaseException() is PostgresException postgres &&
        postgres.SqlState is PostgresErrorCodes.UniqueViolation
            or PostgresErrorCodes.ForeignKeyViolation
            or PostgresErrorCodes.CheckViolation
            or PostgresErrorCodes.SerializationFailure
            or PostgresErrorCodes.DeadlockDetected;

    private void RejectHistoryChanges()
    {
        if (ChangeTracker.Entries<AuditEventDbo>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Сохранённые события аудита нельзя изменять или удалять.");
        if (ChangeTracker.Entries<MedicalRecordHistoryEventDbo>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Сохранённые события истории нельзя изменять или удалять.");
    }

    private void UpdateAccountTimestamps()
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<HospitalUser>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAtUtc == default) entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedAtUtc = entry.Entity.CreatedAtUtc.ToUniversalTime();
                entry.Entity.UpdatedAtUtc = entry.Entity.CreatedAtUtc;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.CreatedAtUtc = entry.Property(x => x.CreatedAtUtc).OriginalValue;
                entry.Property(x => x.CreatedAtUtc).IsModified = false;
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }
}
