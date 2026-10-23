using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Infrastructure.Contexts;
using HospitalInformationSystem.Infrastructure.DI;
using HospitalInformationSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class IdentityBootstrapperConcurrencyTests
{
    [Fact]
    public async Task SaveAdministrator_OrphanMarkerOrReservedRoleDeletion_ShouldRejectMutation()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            await db.Database.MigrateAsync();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<HospitalUser>>();
            var createOrphan = () => users.CreateAsync(new HospitalUser
            {
                Id = Guid.NewGuid(), UserName = "orphan.admin", IsAdministrator = true
            }, "Integration.Guard-Only.6249!");

            await createOrphan.Should().ThrowAsync<BusinessConflictException>();
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            (await db.Users.AnyAsync()).Should().BeFalse();
            await scope.ServiceProvider.GetRequiredService<IdentityBootstrapper>()
                .InitializeAsync("only.admin", "Integration.Guard-Only.6249!", CancellationToken.None);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var administratorRole = (await roles.FindByNameAsync(ApplicationRoles.Administrator))!;
            var deleteAdministratorRole = () => roles.DeleteAsync(administratorRole);

            await deleteAdministratorRole.Should().ThrowAsync<BusinessConflictException>();
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            var user = await db.Users.SingleAsync();
            user.IsAdministrator.Should().BeTrue();
            (await db.Roles.CountAsync()).Should().Be(2);
            (await db.UserRoles.CountAsync()).Should().Be(1);
            var session = await scope.ServiceProvider.GetRequiredService<AccountService>().GetSessionAsync(user.Id, default);
            session!.Role.Should().Be(ApplicationRoles.Administrator);
        }
    }

    [Fact]
    public async Task AssignRole_ConcurrentAdministratorAssignments_ShouldEnforceSingleAdministrator()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            await db.Database.MigrateAsync();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<HospitalUser>>();
            foreach (var (id, name) in new[] { (firstUserId, "direct.first"), (secondUserId, "direct.second") })
            {
                var created = await users.CreateAsync(new HospitalUser { Id = id, UserName = name },
                    "Integration.Direct-Only.6249!");
                created.Succeeded.Should().BeTrue();
            }
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            (await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.Administrator) { Id = roleId }))
                .Succeeded.Should().BeTrue();
        }

        var results = await Task.WhenAll(InsertBindingAsync(firstUserId), InsertBindingAsync(secondUserId));

        results.Count(succeeded => succeeded).Should().Be(1);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            (await db.UserRoles.CountAsync()).Should().Be(1);
            var administrator = await db.Users.SingleAsync(user => user.IsAdministrator);
            var session = await scope.ServiceProvider.GetRequiredService<AccountService>()
                .GetSessionAsync(administrator.Id, default);
            session.Should().NotBeNull();
            session!.Role.Should().Be(ApplicationRoles.Administrator);
        }

        async Task<bool> InsertBindingAsync(Guid userId)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
            try
            {
                await db.SaveChangesAsync();
                return true;
            }
            catch (BusinessConflictException)
            {
                return false;
            }
        }
    }

    [Fact]
    public async Task Bootstrap_ConcurrentEmptyDatabaseInitialization_ShouldCompleteAtomically()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<HospitalDbContext>().Database.MigrateAsync();

        var missingCredentials = () => InitializeAsync(null);
        await missingCredentials.Should().ThrowAsync<InvalidOperationException>();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            (await db.Users.CountAsync()).Should().Be(0);
            (await db.Roles.CountAsync()).Should().Be(0);
        }

        await Task.WhenAll(InitializeAsync("first.admin"), InitializeAsync("second.admin"));

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
            (await db.Users.CountAsync()).Should().Be(1);
            (await db.Users.SingleAsync()).IsAdministrator.Should().BeTrue();
            (await db.Roles.CountAsync()).Should().Be(2);
            (await db.UserRoles.CountAsync()).Should().Be(1);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<HospitalUser>>();
            (await users.GetUsersInRoleAsync(ApplicationRoles.Administrator)).Should().ContainSingle();
        }

        async Task InitializeAsync(string? userName)
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IdentityBootstrapper>()
                .InitializeAsync(userName, userName is null ? null : "Integration.Bootstrap-Only.6249!", CancellationToken.None);
        }
    }
}
