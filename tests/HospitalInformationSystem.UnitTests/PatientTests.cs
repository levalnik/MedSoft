using HospitalInformationSystem.Domain.Patients.Factories;
using HospitalInformationSystem.Domain.Patients.Services;
using System;
using FluentAssertions;
using HospitalInformationSystem.Domain.Common;
using HospitalInformationSystem.Domain.Patients;
using Xunit;

namespace HospitalInformationSystem.UnitTests;

public sealed class PatientTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Create_OptionalText_ShouldNormalizeTextAndPreserveBirthDate()
    {
        var patient = PatientFactory.Create(" Елена ", " Соколова ", "  ", new DateOnly(1985, 3, 12),
            AdministrativeSex.Female, " Аллергия ", Now);

        patient.FirstName.Should().Be("Елена");
        patient.LastName.Should().Be("Соколова");
        patient.MiddleName.Should().BeNull();
        patient.Comment.Should().Be("Аллергия");
        patient.BirthDate.Should().Be(new DateOnly(1985, 3, 12));
        patient.CreatedAtUtc.Offset.Should().Be(TimeSpan.Zero);
        patient.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345")]
    public void Create_InvalidName_ShouldRejectInput(string? name)
    {
        var act = () => PatientFactory.Create(name!, "Соколова", null, new DateOnly(1985, 3, 12),
            AdministrativeSex.Female, null, Now);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Update_InvalidFields_ShouldPreservePatient()
    {
        var patient = PatientFactory.Create("Елена", "Соколова", null, new DateOnly(1985, 3, 12),
            AdministrativeSex.Female, null, Now);
        var act = () => PatientService.Update(patient, "Мария", "Петрова", "Ивановна", new DateOnly(1990, 1, 1),
            AdministrativeSex.Unknown, new string('x', 33), Now.AddDays(1));

        act.Should().Throw<DomainValidationException>();
        patient.FirstName.Should().Be("Елена");
        patient.BirthDate.Should().Be(new DateOnly(1985, 3, 12));
        patient.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Create_UnknownSexValue_ShouldRejectInput()
    {
        var act = () => PatientFactory.Create("Елена", "Соколова", null, new DateOnly(1985, 3, 12),
            (AdministrativeSex)123, null, Now);
        act.Should().Throw<DomainValidationException>();
    }
}
