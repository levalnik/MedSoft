using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HospitalInformationSystem.Infrastructure.Contexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class ConcurrentRecordReadGate : DbCommandInterceptor
{
    private ReadPair? _pair;

    public void Arm(Guid medicalRecordId) => _pair = new ReadPair(medicalRecordId);

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
        CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken)
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
