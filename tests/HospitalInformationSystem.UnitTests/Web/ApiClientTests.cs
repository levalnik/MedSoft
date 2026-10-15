using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Web.ApiClients;
using Xunit;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class ApiClientTests
{
    public static TheoryData<string, string> InvalidSuccessResponses => new()
    {
        { "workers", "[null]" }, { "diagnoses", "[null]" },
        { "workers", "" }, { "workers", "null" }, { "workers", "<html>Proxy error</html>" },
        { "diagnoses", "" }, { "diagnoses", "null" }, { "diagnoses", "<html>Proxy error</html>" },
        { "worker", "" }, { "worker", "null" }, { "worker", "<html>Proxy error</html>" },
        { "save", "" }, { "save", "null" }, { "save", "<html>Proxy error</html>" },
        { "import", "" }, { "import", "null" }, { "import", "<html>Proxy error</html>" },
        { "worker", "{}" }, { "import", "{}" }, { "workers", "[{}]" }, { "diagnoses", "[{}]" },
        {
            "worker",
            "{\"id\":\"00000000-0000-0000-0000-000000000001\",\"firstName\":null,\"lastName\":\"Иванов\",\"position\":\"Врач\",\"isDeleted\":false}"
        }
    };

    [Theory]
    [MemberData(nameof(InvalidSuccessResponses))]
    public async Task Invalid_success_response_becomes_a_displayable_error(string operation, string body)
    {
        using var http = CreateClient(HttpStatusCode.OK, body);

        var action = () => RequestAsync(operation, http);

        await action.Should().ThrowAsync<ApiException>().WithMessage("*некорректный ответ*");
    }

    [Theory]
    [InlineData("workers")]
    [InlineData("diagnoses")]
    [InlineData("worker")]
    [InlineData("save")]
    [InlineData("import")]
    [InlineData("delete")]
    public async Task Validation_problem_displays_unique_errors_and_tolerates_null_entries(string operation)
    {
        using var http = CreateClient(HttpStatusCode.BadRequest,
            """{"title":"Ошибка","errors":{"Code":["Неверный код.","Неверный код."],"Name":null,"Other":[""]}}""");

        var action = () => RequestAsync(operation, http);

        await action.Should().ThrowAsync<ApiException>().WithMessage("Неверный код.");
    }

    [Theory]
    [InlineData("workers", "<html>Bad gateway</html>", "Ошибка сервера: 502.")]
    [InlineData("diagnoses", "", "Ошибка сервера: 502.")]
    [InlineData("import", "{\"detail\":\"Конфликт кодов.\",\"title\":\"Ошибка\"}", "Конфликт кодов.")]
    [InlineData("save", "{\"title\":\"Ошибка сохранения.\"}", "Ошибка сохранения.")]
    [InlineData("delete", "{\"title\":\"\",\"detail\":\" \"}", "Ошибка сервера: 502.")]
    public async Task Server_error_uses_problem_details_or_status_fallback(string operation, string body,
        string message)
    {
        using var http = CreateClient(HttpStatusCode.BadGateway, body);

        var action = () => RequestAsync(operation, http);

        await action.Should().ThrowAsync<ApiException>().WithMessage(message);
    }

    [Fact]
    public async Task Valid_empty_catalog_is_distinct_from_an_invalid_response()
    {
        using var http = CreateClient(HttpStatusCode.OK, "[]");

        var result = await new DiagnosesApiClient(http).ListAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Valid_worker_response_preserves_false_status()
    {
        var id = Guid.NewGuid();
        using var http = CreateClient(HttpStatusCode.OK,
            $$"""{"id":"{{id}}","firstName":"Иван","lastName":"Иванов","position":"Врач","isDeleted":false}""");

        var result = await new MedicalWorkersApiClient(http).GetAsync(id, CancellationToken.None);

        result.Should().Be(new MedicalWorkerDto(id, "Иван", "Иванов", "Врач", false));
    }

    [Fact]
    public async Task Valid_import_response_preserves_zero_counts()
    {
        using var http = CreateClient(HttpStatusCode.OK, """{"created":0,"updated":0,"unchanged":1}""");
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Code,Name\nA00.00,Холера"));

        var result = await new DiagnosesApiClient(http).ImportAsync(stream, "диагнозы.csv", CancellationToken.None);

        result.Created.Should().Be(0);
        result.Updated.Should().Be(0);
        result.Unchanged.Should().Be(1);
    }

    [Fact]
    public async Task Delete_accepts_no_content()
    {
        using var http = CreateClient(HttpStatusCode.NoContent, "");

        await new MedicalWorkersApiClient(http).DeleteAsync(Guid.NewGuid(), CancellationToken.None);
    }

    [Theory]
    [InlineData("workers")]
    [InlineData("import")]
    public async Task Caller_cancellation_is_not_reported_as_an_invalid_response(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var http = CreateClient(HttpStatusCode.OK, "[]");

        var action = () => RequestAsync(operation, http, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static Task
        RequestAsync(string operation, HttpClient http, CancellationToken cancellationToken = default) =>
        operation switch
        {
            "workers" => new MedicalWorkersApiClient(http).ListAsync(true, cancellationToken),
            "worker" => new MedicalWorkersApiClient(http).GetAsync(Guid.NewGuid(), cancellationToken),
            "save" => new MedicalWorkersApiClient(http).SaveAsync(null,
                new SaveMedicalWorkerRequest("Иван", "Иванов", "Терапевт"), cancellationToken),
            "delete" => new MedicalWorkersApiClient(http).DeleteAsync(Guid.NewGuid(), cancellationToken),
            "diagnoses" => new DiagnosesApiClient(http).ListAsync(cancellationToken),
            "import" => new DiagnosesApiClient(http).ImportAsync(
                new MemoryStream(Encoding.UTF8.GetBytes("Code,Name\nA00.00,Холера")), "diagnoses.csv",
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private static HttpClient CreateClient(HttpStatusCode status, string body) =>
        new(new ResponseHandler(status, body)) { BaseAddress = new Uri("http://localhost/") };

    private sealed class ResponseHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(status)
                { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
