using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.Identity;
using HospitalInformationSystem.Infrastructure.Persistence.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class SecurityPersistenceTests(HospitalApiFactory factory) : IClassFixture<HospitalApiFactory>
{
    [Theory]
    [InlineData("first_name", "")]
    [InlineData("last_name", " ")]
    [InlineData("position", "Аб")]
    [InlineData("role", "surgeon")]
    public async Task SaveWorker_InvalidDirectDatabaseValues_ShouldEnforceConstraints(string column, string value)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var worker = await db.MedicalWorkers.AsNoTracking().FirstAsync();
        var sql = column switch
        {
            "first_name" => "UPDATE medical_workers SET first_name = @value WHERE id = @id",
            "last_name" => "UPDATE medical_workers SET last_name = @value WHERE id = @id",
            "position" => "UPDATE medical_workers SET position = @value WHERE id = @id",
            "role" => "UPDATE medical_workers SET role = @value WHERE id = @id",
            _ => throw new ArgumentOutOfRangeException(nameof(column))
        };
        var update = () => db.Database.ExecuteSqlRawAsync(sql,
            new NpgsqlParameter("value", value), new NpgsqlParameter("id", worker.Id));
        (await update.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should()
            .Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task UpdateUser_DirectIdentityUpdate_ShouldStampUtcDatesAndPreserveCreationTime()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<HospitalUser>>();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var user = NewUser();
        var creation = await users.CreateAsync(user);
        creation.Succeeded.Should().BeTrue("Identity errors: {0}",
            string.Join("; ", creation.Errors.Select(error => error.Description)));
        user.CreatedAtUtc.Should().NotBe(default);
        user.CreatedAtUtc.Offset.Should().Be(TimeSpan.Zero);
        user.UpdatedAtUtc.Should().Be(user.CreatedAtUtc);
        user.IsActive.Should().BeTrue();
        var createdAt = user.CreatedAtUtc;

        user.CreatedAtUtc = createdAt.AddDays(10);
        user.IsActive = false;
        (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
        db.ChangeTracker.Clear();
        var persisted = await db.Users.SingleAsync(x => x.Id == user.Id);
        persisted.CreatedAtUtc.Should().Be(createdAt);
        persisted.UpdatedAtUtc.Should().BeOnOrAfter(createdAt);
        persisted.UpdatedAtUtc.Offset.Should().Be(TimeSpan.Zero);
        persisted.IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("abcdefghijklmnopq")]
    public async Task CreateUser_InvalidLength_ShouldRejectOutsideUsernameBoundaries(string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<HospitalUser>>();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var user = NewUser();
        user.UserName = name;
        var result = await users.CreateAsync(user);
        result.Succeeded.Should().BeFalse();
        (await db.Users.AnyAsync(x => x.Id == user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task SaveUser_InvalidNormalizationLengthOrDateOrder_ShouldEnforceConstraints()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<HospitalUser>>();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var user = NewUser();
        var creation = await users.CreateAsync(user);
        creation.Succeeded.Should().BeTrue("Identity errors: {0}",
            string.Join("; ", creation.Errors.Select(error => error.Description)));
        var invalidLength = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AspNetUsers" SET "UserName" = 'ab', "NormalizedUserName" = 'AB' WHERE "Id" = {user.Id}
            """);
        (await invalidLength.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should()
            .Be(PostgresErrorCodes.CheckViolation);
        var invalidNormalization = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AspNetUsers" SET "NormalizedUserName" = 'DIFFERENT' WHERE "Id" = {user.Id}
            """);
        (await invalidNormalization.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should()
            .Be(PostgresErrorCodes.CheckViolation);
        var invalidDate = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AspNetUsers" SET "UpdatedAtUtc" = "CreatedAtUtc" - INTERVAL '1 second' WHERE "Id" = {user.Id}
            """);
        (await invalidDate.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should()
            .Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task CreateAdministrator_ConcurrentRequests_ShouldCommitOneCompleteAccount()
    {
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var roles = setupScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync(ApplicationRoles.Administrator))
                (await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.Administrator)
                    { Id = Guid.NewGuid() })).Succeeded.Should().BeTrue();
        }
        var names = new[] { "adm" + Guid.NewGuid().ToString("N")[..10], "adm" + Guid.NewGuid().ToString("N")[..10] };
        var results = await Task.WhenAll(names.Select(async name =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();
            try
            {
                await accounts.CreateAsync(new CreateAccountRequest(name, "Admin-Password.6482!",
                    ApplicationRoles.Administrator, null), CancellationToken.None);
                return (Exception?)null;
            }
            catch (BusinessConflictException exception)
            {
                return exception;
            }
        }));
        results.Should().ContainSingle(x => x == null);
        results.Should().ContainSingle(x => x is BusinessConflictException);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var administrator = await db.Users.SingleAsync(x => x.IsAdministrator);
        names.Should().Contain(administrator.UserName);
        (await db.Users.CountAsync(x => names.Contains(x.UserName!))).Should().Be(1);
        var roleId = await db.Roles.Where(x => x.Name == ApplicationRoles.Administrator).Select(x => x.Id).SingleAsync();
        (await db.UserRoles.CountAsync(x => x.UserId == administrator.Id && x.RoleId == roleId)).Should().Be(1);
    }

    [Fact]
    public async Task SaveAuditEvent_ExistingEventUpdateOrDelete_ShouldRejectMutation()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var audit = NewAuditEvent();
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync();
        audit.Message = "Changed";
        var edit = () => db.SaveChangesAsync();
        await edit.Should().ThrowAsync<InvalidOperationException>();
        db.ChangeTracker.Clear();

        var saved = await db.AuditEvents.SingleAsync(x => x.EventId == audit.EventId);
        saved.Message.Should().Be("Synthetic security persistence test");
        db.AuditEvents.Remove(saved);
        var delete = () => db.SaveChangesAsync();
        await delete.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteSql_ExistingAuditEventUpdateOrDelete_ShouldRejectMutation()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var audit = NewAuditEvent();
        db.AuditEvents.Add(audit);
        await db.SaveChangesAsync();
        var edit = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE security_audit_events SET message = 'Changed' WHERE event_id = {audit.EventId}
            """);
        (await edit.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55000");
        var delete = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM security_audit_events WHERE event_id = {audit.EventId}
            """);
        (await delete.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55000");
        (await db.AuditEvents.AsNoTracking().SingleAsync(x => x.EventId == audit.EventId)).Message
            .Should().Be("Synthetic security persistence test");
    }

    [Fact]
    public async Task SaveSetupState_MissingAdministratorOrTotp_ShouldEnforceSingletonAndCompletionRequirements()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var now = DateTimeOffset.UtcNow;
        if (!await db.AdministratorSetupStates.AnyAsync())
        {
            db.AdministratorSetupStates.Add(new AdministratorSetupStateDbo { CreatedAtUtc = now, UpdatedAtUtc = now });
            await db.SaveChangesAsync();
        }
        var secondState = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO administrator_setup_state (id, is_completed, created_at_utc, updated_at_utc)
            VALUES (2, false, {now}, {now})
            """);
        (await secondState.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should()
            .Be(PostgresErrorCodes.CheckViolation);
        var incompleteSetup = () => db.Database.ExecuteSqlRawAsync("""
            UPDATE administrator_setup_state SET is_completed = true, administrator_user_id = NULL,
                totp_secret_protected = NULL, completed_at_utc = NULL WHERE id = 1
            """);
        (await incompleteSetup.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should()
            .Be(PostgresErrorCodes.CheckViolation);
        (await db.AdministratorSetupStates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SaveRefreshSession_InvalidHashOwnerOrExpiration_ShouldEnforceConstraints()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<HospitalUser>>();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var user = NewUser();
        var creation = await users.CreateAsync(user);
        creation.Succeeded.Should().BeTrue("Identity errors: {0}",
            string.Join("; ", creation.Errors.Select(error => error.Description)));
        var session = new RefreshSessionDbo
        {
            Id = Guid.NewGuid(), UserId = user.Id, FamilyId = Guid.NewGuid(),
            TokenHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7)
        };
        db.RefreshSessions.Add(session);
        await db.SaveChangesAsync();
        var duplicateHash = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO refresh_sessions (id, user_id, family_id, token_hash, created_at_utc, expires_at_utc)
            VALUES ({Guid.NewGuid()}, {user.Id}, {session.FamilyId}, {session.TokenHash},
                {session.CreatedAtUtc}, {session.ExpiresAtUtc})
            """);
        (await duplicateHash.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should()
            .Be(PostgresErrorCodes.UniqueViolation);
        var invalidHash = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE refresh_sessions SET token_hash = 'plain-token' WHERE id = {session.Id}
            """);
        (await invalidHash.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should()
            .Be(PostgresErrorCodes.CheckViolation);
        var missingOwner = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE refresh_sessions SET user_id = {Guid.NewGuid()} WHERE id = {session.Id}
            """);
        (await missingOwner.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should()
            .Be(PostgresErrorCodes.ForeignKeyViolation);
        var invalidExpiration = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE refresh_sessions SET expires_at_utc = created_at_utc WHERE id = {session.Id}
            """);
        (await invalidExpiration.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should()
            .Be(PostgresErrorCodes.CheckViolation);
    }

    private static HospitalUser NewUser() => new()
    {
        Id = Guid.NewGuid(), UserName = "sec" + Guid.NewGuid().ToString("N")[..10], LockoutEnabled = true
    };

    private static AuditEventDbo NewAuditEvent() => new()
    {
        EventId = Guid.NewGuid(), EventTimeUtc = DateTimeOffset.UtcNow, EventType = "security.persistence-test",
        ActorKind = "system", ActorDisplayName = "Integration test", Message = "Synthetic security persistence test"
    };
}
