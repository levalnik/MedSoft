using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Diagnoses;
using HospitalInformationSystem.Contracts.Patients;
using HospitalInformationSystem.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalInformationSystem.IntegrationTests;

public sealed class PatientsAndDiagnosesApiTests(HospitalApiFactory factory) : IClassFixture<HospitalApiFactory>
{
    private readonly HttpClient _client = factory.CreateBusinessClient();

    [Fact]
    public async Task Patient_update_is_persisted_and_patient_without_record_is_physically_deleted()
    {
        var response = await _client.PostAsJsonAsync("/api/patients", new SavePatientRequest(
            " Елена ", "Соколова", " ", new DateOnly(1985, 3, 12), AdministrativeSex.Female, " Аллергия "));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var patient = (await response.Content.ReadFromJsonAsync<PatientDto>())!;
        response.Headers.Location!.ToString().Should().EndWith($"/api/patients/{patient.Id}");
        patient.FirstName.Should().Be("Елена");
        patient.MiddleName.Should().BeNull();
        patient.HasMedicalRecord.Should().BeFalse();

        var update = await _client.PutAsJsonAsync($"/api/patients/{patient.Id}", new SavePatientRequest(
            "Мария", "Петрова", "Ивановна", new DateOnly(1990, 1, 1), AdministrativeSex.Unknown, "Обновлено"));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = (await _client.GetFromJsonAsync<PatientDto>($"/api/patients/{patient.Id}"))!;
        saved.FirstName.Should().Be("Мария");
        saved.BirthDate.Should().Be(new DateOnly(1990, 1, 1));
        saved.Sex.Should().Be(AdministrativeSex.Unknown);
        (await _client.GetFromJsonAsync<PatientDto[]>("/api/patients"))!
            .Should().Contain(x => x.Id == patient.Id && x.Comment == "Обновлено");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var dbo = await db.Patients.AsNoTracking().SingleAsync(x => x.Id == patient.Id);
        dbo.MiddleName.Should().Be("Ивановна");
        dbo.Sex.Should().Be(2);
        dbo.UpdatedAtUtc.Should().BeOnOrAfter(dbo.CreatedAtUtc);

        (await _client.DeleteAsync($"/api/patients/{patient.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await db.Patients.AnyAsync(x => x.Id == patient.Id)).Should().BeFalse();
        (await _client.GetAsync($"/api/patients/{patient.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(null, null, "Female")]
    [InlineData(" ", null, "Female")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345", null, "Female")]
    [InlineData("Елена", "123456789012345678901234567890123", "Female")]
    [InlineData("Елена", null, "999")]
    [InlineData("Елена", null, "InvalidSex")]
    public async Task Invalid_patient_fields_or_sex_return_400_without_inserting(string? firstName, string? comment,
        string sex)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var count = await db.Patients.CountAsync();
        object sexValue = int.TryParse(sex, out var number) ? number : sex;
        var response = await _client.PostAsJsonAsync("/api/patients", new
        {
            FirstName = firstName, LastName = "Соколова", MiddleName = (string?)null,
            BirthDate = "1985-03-12", Sex = sexValue, Comment = comment
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status.Should().Be(400);
        (await db.Patients.CountAsync()).Should().Be(count);
    }

    [Theory]
    [InlineData("A00.0", "Холера")]
    [InlineData("A00.000", "Холера")]
    [InlineData("A00.00", "АБ")]
    [InlineData("A00.00", "1234567890123456789012345")]
    public async Task Invalid_diagnosis_lengths_return_validation_problem_without_inserting(string code, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var count = await db.DiagnosisDefinitions.CountAsync();
        var response = await _client.PostAsJsonAsync("/api/diagnoses", new SaveDiagnosisRequest(code, name));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Should().NotBeEmpty();
        (await db.DiagnosisDefinitions.CountAsync()).Should().Be(count);
    }

    [Fact]
    public async Task Diagnosis_uniqueness_conflicts_return_409_and_successful_update_and_delete_are_persisted()
    {
        var token = Guid.NewGuid().ToString("N");
        var code = token[..6];
        var name = "Диагноз " + token[..8];
        var create = await _client.PostAsJsonAsync("/api/diagnoses",
            new SaveDiagnosisRequest(" " + code + " ", " " + name + " "));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var diagnosis = (await create.Content.ReadFromJsonAsync<DiagnosisDefinitionDto>())!;
        diagnosis.Code.Should().Be(code);
        diagnosis.Name.Should().Be(name);

        var duplicateCode =
            await _client.PostAsJsonAsync("/api/diagnoses", new SaveDiagnosisRequest(code, "Другое " + token[..8]));
        duplicateCode.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await duplicateCode.Content.ReadFromJsonAsync<ProblemDetails>())!.Status.Should().Be(409);
        var duplicateName =
            await _client.PostAsJsonAsync("/api/diagnoses", new SaveDiagnosisRequest(token[8..14], name));
        duplicateName.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updatedName = "Изменен " + token[..8];
        (await _client.PutAsJsonAsync($"/api/diagnoses/{diagnosis.Id}", new SaveDiagnosisRequest(code, updatedName)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetFromJsonAsync<DiagnosisDefinitionDto>($"/api/diagnoses/{diagnosis.Id}"))!
            .Name.Should().Be(updatedName);
        (await _client.GetFromJsonAsync<DiagnosisDefinitionDto[]>("/api/diagnoses"))!
            .Should().Contain(x => x.Id == diagnosis.Id && x.Name == updatedName);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        (await db.DiagnosisDefinitions.AsNoTracking().SingleAsync(x => x.Id == diagnosis.Id)).Name.Should()
            .Be(updatedName);
        (await _client.DeleteAsync($"/api/diagnoses/{diagnosis.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await db.DiagnosisDefinitions.AnyAsync(x => x.Id == diagnosis.Id)).Should().BeFalse();
    }

    [Theory]
    [InlineData("patients", "GET")]
    [InlineData("patients", "PUT")]
    [InlineData("patients", "DELETE")]
    [InlineData("diagnoses", "GET")]
    [InlineData("diagnoses", "PUT")]
    [InlineData("diagnoses", "DELETE")]
    public async Task Unknown_entity_returns_404_problem(string route, string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/{route}/{Guid.NewGuid()}");
        if (method == "PUT")
            request.Content = route == "patients"
                ? JsonContent.Create(new SavePatientRequest("Елена", "Соколова", null,
                    new DateOnly(1985, 3, 12), AdministrativeSex.Female, null))
                : JsonContent.Create(new SaveDiagnosisRequest("A00.00", "Холера"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status.Should().Be(404);
    }
}
