using HospitalInformationSystem.Domain.Diagnoses.Factories;
using HospitalInformationSystem.Domain.Diagnoses.Services;
using System;
using FluentAssertions;
using HospitalInformationSystem.Domain.Common;
using HospitalInformationSystem.Domain.Diagnoses;
using Xunit;

namespace HospitalInformationSystem.UnitTests;

public sealed class DiagnosisDefinitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null, "Холера")]
    [InlineData("A00.0", "Холера")]
    [InlineData("A00.000", "Холера")]
    [InlineData("A00.00", null)]
    [InlineData("A00.00", "АБ")]
    [InlineData("A00.00", "1234567890123456789012345")]
    public void Create_InvalidCodeOrName_ShouldRejectInput(string? code, string? name)
    {
        var act = () => DiagnosisDefinitionFactory.Create(code!, name!, Now);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Update_ValidFields_ShouldNormalizeTextAndPreserveIdentityAndCreationTime()
    {
        var diagnosis = DiagnosisDefinitionFactory.Create(" A00.00 ", " Холера ", Now);
        var id = diagnosis.Id;
        DiagnosisDefinitionService.Update(diagnosis, " B01.00 ", " Ветряная оспа ", Now.AddHours(1));

        diagnosis.Code.Should().Be("B01.00");
        diagnosis.Name.Should().Be("Ветряная оспа");
        diagnosis.Id.Should().Be(id);
        diagnosis.CreatedAtUtc.Should().Be(Now);
        diagnosis.UpdatedAtUtc.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Update_InvalidFields_ShouldPreserveCodeAndTimestamp()
    {
        var diagnosis = DiagnosisDefinitionFactory.Create("A00.00", "Холера", Now);
        var act = () => DiagnosisDefinitionService.Update(diagnosis, "B01.00", " ", Now.AddHours(1));

        act.Should().Throw<DomainValidationException>();
        diagnosis.Code.Should().Be("A00.00");
        diagnosis.UpdatedAtUtc.Should().Be(Now);
    }
}
