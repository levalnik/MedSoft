using System.Data.Common;
using HospitalInformationSystem.Infrastructure.Contexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class MedicalRecordsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("medical_records_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public TestTimeProvider Clock { get; } = new();
    public ConcurrentRecordReadGate RecordReads { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Database:ApplyMigrations", "true");
        builder.UseSetting("Database:Seed", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddDbContext<HospitalDbContext>(options => options.AddInterceptors(RecordReads));
        });
    }

    public Task InitializeAsync() => _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

// Задерживает уже выполненные чтения до получения обоими запросами одной версии.
// Так проверка конфликта не зависит от того, успел ли первый HTTP-запрос завершиться раньше второго.
public sealed class ConcurrentRecordReadGate : DbCommandInterceptor
{
    private ReadPair? _pair;

    public void Arm(Guid medicalRecordId) => _pair = new ReadPair(medicalRecordId);

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
        CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        var pair = _pair;
        if (pair is not null && command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains("medical_records", StringComparison.Ordinal) &&
            command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is Guid id && id == pair.RecordId))
        {
            var position = Interlocked.Increment(ref pair.ReadCount);
            if (position == 2) pair.Ready.TrySetResult();
            if (position <= 2) await pair.Ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        return result;
    }

    private sealed class ReadPair(Guid recordId)
    {
        public Guid RecordId { get; } = recordId;
        public int ReadCount;
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public sealed class TestTimeProvider : TimeProvider
{
    private long _utcTicks = new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero).UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);

    public void SetUtcNow(DateTimeOffset value) => Interlocked.Exchange(ref _utcTicks, value.UtcTicks);
}
