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

public sealed class MedicalRecordReadProbe : DbCommandInterceptor
{
    private ReadObservation? _observation;

    public ReadObservation Arm(Guid recordId) => _observation = new ReadObservation(recordId);

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
        CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken)
    {
        var observation = _observation;
        if (observation is null || !command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
            !command.CommandText.Contains("medical_records", StringComparison.Ordinal) ||
            !command.Parameters.Cast<DbParameter>()
                .Any(parameter => parameter.Value is Guid id && id == observation.RecordId))
            return result;

        var contextId = eventData.Context!.ContextId.InstanceId;
        bool first;
        lock (observation.Commands)
        {
            observation.ContextId ??= contextId;
            if (observation.ContextId != contextId) return result;
            observation.Commands.Add(command.CommandText);
            first = observation.Commands.Count == 1;
        }

        if (first)
        {
            observation.HeaderRead.TrySetResult();
            await observation.Resume.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }

        return result;
    }

    public sealed class ReadObservation(Guid recordId) : IDisposable
    {
        public Guid RecordId { get; } = recordId;
        public Guid? ContextId { get; set; }
        public List<string> Commands { get; } = [];
        public TaskCompletionSource HeaderRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Dispose() => Resume.TrySetResult();
    }
}
