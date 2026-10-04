using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HospitalInformationSystem.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class MedicalRecordsApiTests(MedicalRecordsApiFactory factory)
    : IClassFixture<MedicalRecordsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly DateTimeOffset T0 = new(2026, 10, 4, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Snapshots_replay_T0_to_T4_and_preserve_history_after_catalog_and_author_changes()
    {
        factory.Clock.SetUtcNow(T0);
        var worker = await CreateWorker();
        var patient = await CreatePatient();
        var definition = await CreateDiagnosis();
        var record = await CreateRecord(patient, worker);

        factory.Clock.SetUtcNow(T0.AddMinutes(1));
        var diagnosis = await AddDiagnosis(record.Id, definition, worker);
        factory.Clock.SetUtcNow(T0.AddMinutes(2));
        var prescription = await AddPrescription(record.Id, "Первое назначение", worker);
        factory.Clock.SetUtcNow(T0.AddMinutes(3));
        await Expect(await Send(HttpMethod.Delete, $"/api/medical-records/{record.Id}/diagnoses/{diagnosis.Id}", worker), HttpStatusCode.NoContent);
        factory.Clock.SetUtcNow(T0.AddMinutes(4));
        var replacement = await Read<PrescriptionView>(await Send(HttpMethod.Put,
            $"/api/medical-records/{record.Id}/prescriptions/{prescription.Id}", worker,
            new { Text = "Второе назначение" }), HttpStatusCode.OK);
        replacement.Id.Should().NotBe(prescription.Id);
        replacement.ReplacesPrescriptionId.Should().Be(prescription.Id);

        var snapshots = new List<SnapshotView>();
        for (var minute = 0; minute <= 4; minute++)
            snapshots.Add(await Snapshot(record.Id, T0.AddMinutes(minute)));
        snapshots.Select(x => x.Version).Should().Equal(1, 2, 3, 4, 6);
        snapshots[0].Diagnoses.Should().BeEmpty();
        snapshots[0].Prescriptions.Should().BeEmpty();
        snapshots[1].Diagnoses.Should().ContainSingle(x => x.Id == diagnosis.Id);
        snapshots[1].Prescriptions.Should().BeEmpty();
        snapshots[2].Diagnoses.Should().ContainSingle(x => x.Id == diagnosis.Id);
        snapshots[2].Prescriptions.Should().ContainSingle(x => x.Id == prescription.Id);
        snapshots[3].Diagnoses.Should().BeEmpty();
        snapshots[3].Prescriptions.Should().ContainSingle(x => x.Id == prescription.Id);
        snapshots[4].Diagnoses.Should().BeEmpty();
        snapshots[4].Prescriptions.Should().ContainSingle(x => x.Id == replacement.Id && x.Text == "Второе назначение");
        (await Snapshot(record.Id, T0.AddDays(1))).Should().BeEquivalentTo(snapshots[4], options => options.Excluding(x => x.At));
        (await Snapshot(record.Id, T0.AddMinutes(2).ToOffset(TimeSpan.FromHours(3)))).Should()
            .BeEquivalentTo(snapshots[2], options => options.Excluding(x => x.At));

        var history = await History(record.Id);
        history.Select(x => x.Version).Should().Equal(1, 2, 3, 4, 5, 6);
        history.Select(x => x.EventType).Should().Equal("MedicalRecordCreated", "DiagnosisAdded", "PrescriptionAdded",
            "DiagnosisRemoved", "PrescriptionCancelled", "PrescriptionAdded");
        history[4].OccurredAtUtc.Should().Be(history[5].OccurredAtUtc);
        history.Should().OnlyContain(x => x.PerformedByMedicalWorkerId == worker && x.PerformedByPosition == "Врач");
        foreach (var item in history)
        {
            using var payload = JsonDocument.Parse(item.PayloadJson);
            payload.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        }

        await Expect(await _client.PutAsJsonAsync($"/api/diagnoses/{definition}",
            new { Code = "ZZZ999", Name = "Название после" }), HttpStatusCode.OK);
        await Expect(await _client.DeleteAsync($"/api/diagnoses/{definition}"), HttpStatusCode.NoContent);
        await Expect(await _client.PutAsJsonAsync($"/api/medical-workers/{worker}",
            new { FirstName = "Николай", LastName = "Новиков", Position = "Хирург" }), HttpStatusCode.OK);
        await Expect(await _client.DeleteAsync($"/api/medical-workers/{worker}"), HttpStatusCode.NoContent);

        (await History(record.Id)).Should().Equal(history);
        (await Snapshot(record.Id, T0.AddMinutes(2))).Should().BeEquivalentTo(snapshots[2]);
        (await Read<RecordView>(await _client.GetAsync($"/api/patients/{patient}/medical-record"), HttpStatusCode.OK))
            .Prescriptions.Should().ContainSingle(x => x.Id == replacement.Id);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deleted")]
    public async Task Every_write_rejects_invalid_author_without_changing_state_or_history(string kind)
    {
        var worker = await CreateWorker();
        var patient = await CreatePatient();
        var emptyPatient = await CreatePatient();
        var record = await CreateRecord(patient, worker);
        var definition = await CreateDiagnosis();
        var diagnosis = await AddDiagnosis(record.Id, definition, worker);
        var prescription = await AddPrescription(record.Id, "Исходное назначение", worker);
        var before = await Current(record.Id);
        var historyBefore = await History(record.Id);
        var invalidAuthor = kind switch
        {
            "missing" => null,
            "malformed" => "not-a-guid",
            "unknown" => Guid.NewGuid().ToString(),
            _ => worker.ToString()
        };
        if (kind == "deleted")
            await Expect(await _client.DeleteAsync($"/api/medical-workers/{worker}"), HttpStatusCode.NoContent);

        var operations = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Post, $"/api/patients/{emptyPatient}/medical-record", null),
            (HttpMethod.Post, $"/api/medical-records/{record.Id}/diagnoses", new { DiagnosisId = definition }),
            (HttpMethod.Delete, $"/api/medical-records/{record.Id}/diagnoses/{diagnosis.Id}", null),
            (HttpMethod.Post, $"/api/medical-records/{record.Id}/prescriptions", new { Text = "Ещё назначение" }),
            (HttpMethod.Post, $"/api/medical-records/{record.Id}/prescriptions/{prescription.Id}/cancel", null),
            (HttpMethod.Put, $"/api/medical-records/{record.Id}/prescriptions/{prescription.Id}", new { Text = "Замена назначения" })
        };
        foreach (var operation in operations)
        {
            var response = await SendWithHeader(operation.Method, operation.Url, invalidAuthor, operation.Body);
            await Expect(response, HttpStatusCode.BadRequest);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status.Should().Be(400);
        }
        (await Current(record.Id)).Should().BeEquivalentTo(before);
        (await History(record.Id)).Should().Equal(historyBefore);
        await Expect(await _client.GetAsync($"/api/patients/{emptyPatient}/medical-record"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Missing_resources_and_children_belonging_to_other_record_return_404_without_mutations()
    {
        var worker = await CreateWorker();
        var record = await CreateRecord(await CreatePatient(), worker);
        var other = await CreateRecord(await CreatePatient(), worker);
        var diagnosis = await AddDiagnosis(record.Id, await CreateDiagnosis(), worker);
        var prescription = await AddPrescription(record.Id, "Исходное назначение", worker);
        var firstBefore = await Current(record.Id);
        var otherBefore = await Current(other.Id);
        await Expect(await Send(HttpMethod.Delete, $"/api/medical-records/{other.Id}/diagnoses/{diagnosis.Id}", worker), HttpStatusCode.NotFound);
        await Expect(await Send(HttpMethod.Post, $"/api/medical-records/{other.Id}/prescriptions/{prescription.Id}/cancel", worker), HttpStatusCode.NotFound);
        await Expect(await Send(HttpMethod.Put, $"/api/medical-records/{other.Id}/prescriptions/{prescription.Id}", worker,
            new { Text = "Новое назначение" }), HttpStatusCode.NotFound);
        await Expect(await Send(HttpMethod.Post, $"/api/patients/{Guid.NewGuid()}/medical-record", worker), HttpStatusCode.NotFound);
        await Expect(await Send(HttpMethod.Post, $"/api/medical-records/{record.Id}/diagnoses", worker,
            new { DiagnosisId = Guid.NewGuid() }), HttpStatusCode.NotFound);
        foreach (var suffix in new[] { "", "/history", "/snapshot?at=2026-10-04T12:00:00Z" })
            await Expect(await _client.GetAsync($"/api/medical-records/{Guid.NewGuid()}{suffix}"), HttpStatusCode.NotFound);
        (await Current(record.Id)).Should().BeEquivalentTo(firstBefore);
        (await Current(other.Id)).Should().BeEquivalentTo(otherBefore);
        (await History(record.Id)).Length.Should().Be((int)firstBefore.Version);
        (await History(other.Id)).Length.Should().Be((int)otherBefore.Version);
    }

    [Fact]
    public async Task Invalid_replacement_does_not_cancel_original_and_cancelled_prescription_cannot_be_replaced()
    {
        var worker = await CreateWorker();
        var record = await CreateRecord(await CreatePatient(), worker);
        var prescription = await AddPrescription(record.Id, "Исходное назначение", worker);
        foreach (var text in new[] { " ", "Аб", new string('А', 129) })
            await Expect(await Send(HttpMethod.Put, $"/api/medical-records/{record.Id}/prescriptions/{prescription.Id}", worker,
                new { Text = text }), HttpStatusCode.BadRequest);
        var current = await Current(record.Id);
        current.Version.Should().Be(2);
        current.Prescriptions.Should().ContainSingle(x => x.Id == prescription.Id);
        (await History(record.Id)).Should().HaveCount(2);
        await Expect(await Send(HttpMethod.Post, $"/api/medical-records/{record.Id}/prescriptions/{prescription.Id}/cancel", worker), HttpStatusCode.NoContent);
        await Expect(await Send(HttpMethod.Put, $"/api/medical-records/{record.Id}/prescriptions/{prescription.Id}", worker,
            new { Text = "Поздняя замена" }), HttpStatusCode.Conflict);
        (await Current(record.Id)).Prescriptions.Should().BeEmpty();
        (await History(record.Id)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Snapshot_requires_explicit_timezone_and_returns_404_before_record_creation()
    {
        factory.Clock.SetUtcNow(T0);
        var record = await CreateRecord(await CreatePatient(), await CreateWorker());
        await Expect(await _client.GetAsync(SnapshotUrl(record.Id, T0.AddTicks(-10))), HttpStatusCode.NotFound);
        foreach (var value in new[] { "2026-10-04T12:00:00", "2026-10-04", "invalid", "" })
            await Expect(await _client.GetAsync($"/api/medical-records/{record.Id}/snapshot?at={Uri.EscapeDataString(value)}"), HttpStatusCode.BadRequest);
        (await Snapshot(record.Id, T0)).Version.Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_record_creation_has_one_winner_and_patient_cannot_be_deleted_afterwards()
    {
        var worker = await CreateWorker();
        var patient = await CreatePatient();
        var responses = await Task.WhenAll(
            Send(HttpMethod.Post, $"/api/patients/{patient}/medical-record", worker),
            Send(HttpMethod.Post, $"/api/patients/{patient}/medical-record", worker));
        responses.Select(x => x.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        var record = await Read<RecordView>(responses.Single(x => x.StatusCode == HttpStatusCode.Created), HttpStatusCode.Created);
        (await History(record.Id)).Should().ContainSingle(x => x.Version == 1);
        await Expect(await _client.DeleteAsync($"/api/patients/{patient}"), HttpStatusCode.Conflict);
        var savedPatient = await Read<PatientView>(await _client.GetAsync($"/api/patients/{patient}"), HttpStatusCode.OK);
        savedPatient.HasMedicalRecord.Should().BeTrue();
    }

    [Fact]
    public async Task Concurrent_additions_keep_state_and_event_versions_consistent()
    {
        var worker = await CreateWorker();
        var record = await CreateRecord(await CreatePatient(), worker);
        var responses = await Task.WhenAll(Enumerable.Range(1, 8).Select(index =>
            Send(HttpMethod.Post, $"/api/medical-records/{record.Id}/prescriptions", worker,
                new { Text = $"Назначение номер {index}" })));
        responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.Created || x.StatusCode == HttpStatusCode.Conflict);
        var successes = responses.Count(x => x.StatusCode == HttpStatusCode.Created);
        successes.Should().BePositive();
        var current = await Current(record.Id);
        var history = await History(record.Id);
        current.Prescriptions.Should().HaveCount(successes);
        current.Version.Should().Be(successes + 1);
        history.Select(x => x.Version).Should().Equal(Enumerable.Range(1, successes + 1).Select(x => (long)x));
        history.Where(x => x.EventType == "PrescriptionAdded").Select(x => x.EntityId)
            .Should().BeEquivalentTo(current.Prescriptions.Select(x => x.Id));
        (await Snapshot(record.Id, factory.Clock.GetUtcNow())).Prescriptions.Should().BeEquivalentTo(current.Prescriptions);
    }

    [Fact]
    public async Task Commands_reading_same_version_cannot_both_commit()
    {
        var worker = await CreateWorker();
        var record = await CreateRecord(await CreatePatient(), worker);
        factory.RecordReads.Arm(record.Id);
        var responses = await Task.WhenAll(
            Send(HttpMethod.Post, $"/api/medical-records/{record.Id}/prescriptions", worker, new { Text = "Первое назначение" }),
            Send(HttpMethod.Post, $"/api/medical-records/{record.Id}/prescriptions", worker, new { Text = "Второе назначение" }));
        responses.Select(x => x.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        var current = await Current(record.Id);
        current.Version.Should().Be(2);
        current.Prescriptions.Should().ContainSingle();
        var history = await History(record.Id);
        history.Select(x => x.Version).Should().Equal(1, 2);
        history[1].EntityId.Should().Be(current.Prescriptions.Single().Id);
    }

    [Fact]
    public async Task Concurrent_replacements_cancel_once_and_commit_only_one_successor()
    {
        var worker = await CreateWorker();
        var record = await CreateRecord(await CreatePatient(), worker);
        var original = await AddPrescription(record.Id, "Исходное назначение", worker);
        var url = $"/api/medical-records/{record.Id}/prescriptions/{original.Id}";
        var responses = await Task.WhenAll(
            Send(HttpMethod.Put, url, worker, new { Text = "Первая замена" }),
            Send(HttpMethod.Put, url, worker, new { Text = "Вторая замена" }));
        responses.Select(x => x.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        var current = await Current(record.Id);
        current.Prescriptions.Should().ContainSingle(x => x.ReplacesPrescriptionId == original.Id);
        current.Version.Should().Be(4);
        var history = await History(record.Id);
        history.Select(x => x.Version).Should().Equal(1, 2, 3, 4);
        history.Should().ContainSingle(x => x.EventType == "PrescriptionCancelled" && x.EntityId == original.Id);
    }

    [Fact]
    public async Task Patient_deletion_racing_record_creation_never_leaves_orphan_or_returns_500()
    {
        var worker = await CreateWorker();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var patient = await CreatePatient();
            var createTask = Send(HttpMethod.Post, $"/api/patients/{patient}/medical-record", worker);
            var deleteTask = _client.DeleteAsync($"/api/patients/{patient}");
            await Task.WhenAll(createTask, deleteTask);
            var create = await createTask;
            var delete = await deleteTask;
            create.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict, HttpStatusCode.NotFound);
            delete.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.Conflict);
            if (create.StatusCode == HttpStatusCode.Created)
            {
                delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
                await Expect(await _client.GetAsync($"/api/patients/{patient}"), HttpStatusCode.OK);
                var record = await Read<RecordView>(create, HttpStatusCode.Created);
                (await History(record.Id)).Should().HaveCount(1);
            }
            else
            {
                delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
                await Expect(await _client.GetAsync($"/api/patients/{patient}"), HttpStatusCode.NotFound);
                await Expect(await _client.GetAsync($"/api/patients/{patient}/medical-record"), HttpStatusCode.NotFound);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_history_insert_rolls_back_cancellation_replacement_and_version(bool simulateDeadlock)
    {
        var worker = await CreateWorker();
        var record = await CreateRecord(await CreatePatient(), worker);
        var prescription = await AddPrescription(record.Id, "Исходное назначение", worker);
        var before = await Current(record.Id);
        var historyBefore = await History(record.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var historyTable = Table(db, "MedicalRecordHistoryEventDbo");
        var prescriptionTable = Table(db, "PrescriptionDbo");
        var rowCount = await CountRows(db, prescriptionTable, "medical_record_id", record.Id);
        // Код ошибки выбирается только из двух констант: проверяем и оболочку transient failure Npgsql.
        var createFunctionSql = """
            CREATE FUNCTION reject_test_history_insert() RETURNS trigger AS $$
            BEGIN
              RAISE EXCEPTION 'Deliberate history insertion failure' USING ERRCODE = 'ERROR_CODE';
            END;
            $$ LANGUAGE plpgsql;
            """.Replace("ERROR_CODE", simulateDeadlock ? "40P01" : "23514", StringComparison.Ordinal);
        await db.Database.ExecuteSqlRawAsync(createFunctionSql);
        try
        {
            // Идентификатор таблицы берётся только из EF metadata и экранируется в Table().
            var createTriggerSql = $"CREATE TRIGGER reject_test_history_insert BEFORE INSERT ON {historyTable} FOR EACH ROW EXECUTE FUNCTION reject_test_history_insert()";
            await db.Database.ExecuteSqlRawAsync(createTriggerSql);
            await Expect(await Send(HttpMethod.Put, $"/api/medical-records/{record.Id}/prescriptions/{prescription.Id}", worker,
                new { Text = "Откатываемая замена" }), HttpStatusCode.Conflict);
            (await Current(record.Id)).Should().BeEquivalentTo(before);
            (await History(record.Id)).Should().Equal(historyBefore);
            (await CountRows(db, prescriptionTable, "medical_record_id", record.Id)).Should().Be(rowCount);
        }
        finally
        {
            var dropTriggerSql = $"DROP TRIGGER IF EXISTS reject_test_history_insert ON {historyTable}";
            await db.Database.ExecuteSqlRawAsync(dropTriggerSql);
            await db.Database.ExecuteSqlRawAsync("DROP FUNCTION IF EXISTS reject_test_history_insert()");
        }
        await Read<PrescriptionView>(await Send(HttpMethod.Put, $"/api/medical-records/{record.Id}/prescriptions/{prescription.Id}", worker,
            new { Text = "Успешная замена после" }), HttpStatusCode.OK);
        (await Current(record.Id)).Version.Should().Be(before.Version + 2);
    }

    [Fact]
    public async Task PostgreSql_enforces_unique_versions_unique_patient_restrict_and_jsonb()
    {
        var worker = await CreateWorker();
        var patient = await CreatePatient();
        var record = await CreateRecord(patient, worker);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var recordEntity = Entity(db, "MedicalRecordDbo");
        var historyEntity = Entity(db, "MedicalRecordHistoryEventDbo");
        recordEntity.GetIndexes().Should().Contain(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "PatientId" }));
        historyEntity.GetIndexes().Should().Contain(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "MedicalRecordId", "Version" }));
        historyEntity.FindProperty("PayloadJson")!.GetColumnType().Should().Be("jsonb");
        db.Model.GetEntityTypes().Should().OnlyContain(x => x.ClrType.Namespace == null || !x.ClrType.Namespace.Contains(".Domain."));

        var recordTable = Table(db, "MedicalRecordDbo");
        var historyTable = Table(db, "MedicalRecordHistoryEventDbo");
        var patientTable = Table(db, "PatientDbo");
        await AssertSqlState(db, $"DELETE FROM {patientTable} WHERE id = {{0}}", PostgresErrorCodes.ForeignKeyViolation, patient);
        await AssertSqlState(db, CloneRowSql(recordEntity, recordTable, "id"), PostgresErrorCodes.UniqueViolation, record.Id);
        var findEventSql = $"SELECT id AS \"Value\" FROM {historyTable} WHERE medical_record_id = {{0}}";
        var firstEventId = await db.Database.SqlQueryRaw<Guid>(findEventSql, record.Id).SingleAsync();
        var payloadTypeSql = $"SELECT pg_typeof(payload_json)::text AS \"Value\" FROM {historyTable} WHERE id = {{0}}";
        (await db.Database.SqlQueryRaw<string>(payloadTypeSql, firstEventId).SingleAsync()).Should().Be("jsonb");
        await AssertSqlState(db, CloneRowSql(historyEntity, historyTable, "id"), PostgresErrorCodes.UniqueViolation, firstEventId);
        await AssertSqlState(db, $"UPDATE {historyTable} SET payload_json = 'not-json'::jsonb WHERE id = {{0}}", PostgresErrorCodes.InvalidTextRepresentation, firstEventId);
        (await Current(record.Id)).Version.Should().Be(1);
        (await History(record.Id)).Should().HaveCount(1);
        var prescription = await AddPrescription(record.Id, "Допустимый текст", worker);
        var prescriptionTable = Table(db, "PrescriptionDbo");
        await AssertSqlState(db, $"UPDATE {prescriptionTable} SET text = 'аб' WHERE id = {{0}}",
            PostgresErrorCodes.CheckViolation, prescription.Id);
    }

    private async Task<Guid> CreateWorker() => (await Read<IdView>(await _client.PostAsJsonAsync("/api/medical-workers",
        new { FirstName = "Алексей", LastName = "Орлов", Position = "Врач" }), HttpStatusCode.Created)).Id;

    private async Task<Guid> CreatePatient() => (await Read<IdView>(await _client.PostAsJsonAsync("/api/patients",
        new { FirstName = "Елена", LastName = "Соколова", MiddleName = "Петровна", BirthDate = "1985-03-12", Sex = "Female", Comment = (string?)null }), HttpStatusCode.Created)).Id;

    private async Task<Guid> CreateDiagnosis() => (await Read<IdView>(await _client.PostAsJsonAsync("/api/diagnoses",
        new { Code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), Name = "Диагноз " + Guid.NewGuid().ToString("N")[..8] }), HttpStatusCode.Created)).Id;

    private async Task<RecordView> CreateRecord(Guid patient, Guid worker) =>
        await Read<RecordView>(await Send(HttpMethod.Post, $"/api/patients/{patient}/medical-record", worker), HttpStatusCode.Created);

    private async Task<DiagnosisView> AddDiagnosis(Guid record, Guid definition, Guid worker) =>
        await Read<DiagnosisView>(await Send(HttpMethod.Post, $"/api/medical-records/{record}/diagnoses", worker,
            new { DiagnosisId = definition }), HttpStatusCode.Created);

    private async Task<PrescriptionView> AddPrescription(Guid record, string text, Guid worker) =>
        await Read<PrescriptionView>(await Send(HttpMethod.Post, $"/api/medical-records/{record}/prescriptions", worker,
            new { Text = text }), HttpStatusCode.Created);

    private async Task<RecordView> Current(Guid record) =>
        await Read<RecordView>(await _client.GetAsync($"/api/medical-records/{record}"), HttpStatusCode.OK);

    private async Task<HistoryView[]> History(Guid record) =>
        await Read<HistoryView[]>(await _client.GetAsync($"/api/medical-records/{record}/history"), HttpStatusCode.OK);

    private async Task<SnapshotView> Snapshot(Guid record, DateTimeOffset at) =>
        await Read<SnapshotView>(await _client.GetAsync(SnapshotUrl(record, at)), HttpStatusCode.OK);

    private static string SnapshotUrl(Guid record, DateTimeOffset at) =>
        $"/api/medical-records/{record}/snapshot?at={Uri.EscapeDataString(at.ToString("O"))}";

    private Task<HttpResponseMessage> Send(HttpMethod method, string url, Guid author, object? body = null) =>
        SendWithHeader(method, url, author.ToString(), body);

    private async Task<HttpResponseMessage> SendWithHeader(HttpMethod method, string url, string? author, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (author is not null) request.Headers.Add("X-Medical-Worker-Id", author);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode status)
    {
        await Expect(response, status);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task Expect(HttpResponseMessage response, HttpStatusCode status) =>
        response.StatusCode.Should().Be(status, "response body: {0}", await response.Content.ReadAsStringAsync());

    private static IEntityType Entity(HospitalDbContext db, string name) => db.Model.GetEntityTypes().Single(x => x.ClrType.Name == name);

    private static string Table(HospitalDbContext db, string name) => Quote(Entity(db, name).GetTableName()!);

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static string CloneRowSql(IEntityType entity, string table, string primaryKey)
    {
        var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        var columns = entity.GetProperties().Select(x => x.GetColumnName(store)!).ToArray();
        return $"INSERT INTO {table} ({string.Join(", ", columns.Select(Quote))}) SELECT " +
            string.Join(", ", columns.Select(x => x == primaryKey ? "gen_random_uuid()" : Quote(x))) +
            $" FROM {table} WHERE {Quote(primaryKey)} = {{0}}";
    }

    private static Task<int> CountRows(HospitalDbContext db, string table, string column, Guid id)
    {
        // Идентификаторы экранированы; значения передаются SQL-параметрами.
        var sql = $"SELECT count(*)::int AS \"Value\" FROM {table} WHERE {Quote(column)} = {{0}}";
        return db.Database.SqlQueryRaw<int>(sql, id).SingleAsync();
    }

    private static async Task AssertSqlState(HospitalDbContext db, string sql, string expected, Guid id)
    {
        Func<Task> execute = async () => await db.Database.ExecuteSqlRawAsync(sql, id);
        (await execute.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(expected);
    }

    private sealed record IdView(Guid Id);
    private sealed record PatientView(Guid Id, bool HasMedicalRecord);
    private sealed record DiagnosisView(Guid Id, string Code, string Name, DateTimeOffset AddedAtUtc, Guid AddedByMedicalWorkerId);
    private sealed record PrescriptionView(Guid Id, string Text, DateTimeOffset CreatedAtUtc, Guid CreatedByMedicalWorkerId, Guid? ReplacesPrescriptionId);
    private sealed record RecordView(Guid Id, Guid PatientId, DateTimeOffset CreatedAtUtc, long Version, DiagnosisView[] Diagnoses, PrescriptionView[] Prescriptions);
    private sealed record SnapshotView(Guid MedicalRecordId, Guid PatientId, DateTimeOffset At, long Version, DiagnosisView[] Diagnoses, PrescriptionView[] Prescriptions);
    private sealed record HistoryView(long Version, DateTimeOffset OccurredAtUtc, Guid PerformedByMedicalWorkerId, string PerformedByName, string PerformedByPosition, string EventType, Guid? EntityId, string PayloadJson);
}
