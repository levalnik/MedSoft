using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using HospitalInformationSystem.Contracts.MedicalRecords;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Contracts.Patients;
using HospitalInformationSystem.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class ApiInputValidationTests(HospitalApiFactory factory) : IClassFixture<HospitalApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("medical-workers", "firstName")]
    [InlineData("medical-workers", "lastName")]
    [InlineData("medical-workers", "position")]
    [InlineData("diagnoses", "code")]
    [InlineData("diagnoses", "name")]
    [InlineData("patients", "firstName")]
    [InlineData("patients", "lastName")]
    [InlineData("patients", "middleName")]
    [InlineData("patients", "comment")]
    public async Task Null_characters_return_400_without_creating_rows(string route, string field)
    {
        var request = ValidRequest(route);
        request[field] = field == "code" ? "A0\0.00" : "Тест\0";
        var before = await _client.GetStringAsync($"/api/{route}");
        var response = await _client.PostAsJsonAsync($"/api/{route}", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Contain("нулевой символ");
        (await _client.GetStringAsync($"/api/{route}")).Should().Be(before);
    }

    [Theory]
    [InlineData("birthDate", "POST")]
    [InlineData("sex", "POST")]
    [InlineData("birthDate", "PUT")]
    [InlineData("sex", "PUT")]
    public async Task Missing_required_patient_fields_do_not_silently_replace_them_with_defaults(string field, string method)
    {
        var data = ValidRequest("patients");
        var route = "/api/patients";
        PatientDto? before = null;
        if (method == "PUT")
        {
            var created = await _client.PostAsJsonAsync(route, data);
            before = (await created.Content.ReadFromJsonAsync<PatientDto>())!;
            route += "/" + before.Id;
        }
        var listBefore = await _client.GetStringAsync("/api/patients");
        data.Remove(field);
        using var request = new HttpRequestMessage(new HttpMethod(method), route) { Content = JsonContent.Create(data) };
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetStringAsync("/api/patients")).Should().Be(listBefore);
    }

    [Fact]
    public async Task Patient_optional_fields_may_still_be_omitted_and_unknown_sex_is_explicit()
    {
        var response = await _client.PostAsJsonAsync("/api/patients", new
        {
            firstName = "Тест", lastName = "Пациент", birthDate = "2000-01-01", sex = "Unknown"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var patient = (await response.Content.ReadFromJsonAsync<PatientDto>())!;
        patient.Sex.Should().Be(AdministrativeSex.Unknown);
        patient.MiddleName.Should().BeNull();
        patient.Comment.Should().BeNull();
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/html")]
    [InlineData("application/xml")]
    public async Task Unsupported_accept_does_not_turn_validation_or_not_found_into_server_error(string accept)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/diagnoses")
        {
            Content = JsonContent.Create(new { code = "BAD", name = "Диагноз" })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Should().ContainKey("Code");

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/patients/{Guid.NewGuid()}");
        get.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        var missing = await _client.SendAsync(get);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await missing.Content.ReadFromJsonAsync<ProblemDetails>())!.Status.Should().Be(404);
    }

    [Fact]
    public async Task Several_uploaded_files_are_rejected_without_silently_importing_only_the_first()
    {
        var before = await _client.GetStringAsync("/api/diagnoses");
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Code,Name\nF10001,Первый")), "file", "first.csv");
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Code,Name\nF10002,Второй")), "file", "second.csv");
        var response = await _client.PostAsync("/api/diagnoses/import", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetStringAsync("/api/diagnoses")).Should().Be(before);
    }

    [Fact]
    public async Task Invalid_prescription_text_does_not_change_record_or_history()
    {
        var authorResponse = await _client.PostAsJsonAsync("/api/medical-workers", new SaveMedicalWorkerRequest("Алексей", "Проверочный", "Врач"));
        var author = (await authorResponse.Content.ReadFromJsonAsync<MedicalWorkerDto>())!;
        var patientResponse = await _client.PostAsJsonAsync("/api/patients", ValidRequest("patients"));
        var patient = (await patientResponse.Content.ReadFromJsonAsync<PatientDto>())!;
        using var create = new HttpRequestMessage(HttpMethod.Post, $"/api/patients/{patient.Id}/medical-record");
        create.Headers.Add("X-Medical-Worker-Id", author.Id.ToString());
        var created = await _client.SendAsync(create);
        var record = (await created.Content.ReadFromJsonAsync<MedicalRecordDto>())!;
        var before = await _client.GetStringAsync($"/api/medical-records/{record.Id}");
        var history = await _client.GetStringAsync($"/api/medical-records/{record.Id}/history");
        using var add = new HttpRequestMessage(HttpMethod.Post, $"/api/medical-records/{record.Id}/prescriptions")
        {
            Content = JsonContent.Create(new { text = "Тест\0" })
        };
        add.Headers.Add("X-Medical-Worker-Id", author.Id.ToString());
        var response = await _client.SendAsync(add);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetStringAsync($"/api/medical-records/{record.Id}")).Should().Be(before);
        (await _client.GetStringAsync($"/api/medical-records/{record.Id}/history")).Should().Be(history);
    }

    private static Dictionary<string, object> ValidRequest(string route) => route switch
    {
        "medical-workers" => new() { ["firstName"] = "Алексей", ["lastName"] = "Проверочный", ["position"] = "Врач" },
        "diagnoses" => new() { ["code"] = "N00.00", ["name"] = "Проверка" },
        _ => new()
        {
            ["firstName"] = "Елена", ["lastName"] = "Проверочная", ["birthDate"] = "2000-01-01", ["sex"] = "Female"
        }
    };
}
