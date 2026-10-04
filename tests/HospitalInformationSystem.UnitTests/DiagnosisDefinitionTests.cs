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
    public void Invalid_code_or_name_is_rejected(string? code, string? name)
    {
        var act = () => DiagnosisDefinition.Create(code!, name!, Now);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Update_normalizes_text_and_preserves_identity_and_creation_time()
    {
        var diagnosis = DiagnosisDefinition.Create(" A00.00 ", " Холера ", Now);
        var id = diagnosis.Id;
        diagnosis.Update(" B01.00 ", " Ветряная оспа ", Now.AddHours(1));

        diagnosis.Code.Should().Be("B01.00");
        diagnosis.Name.Should().Be("Ветряная оспа");
        diagnosis.Id.Should().Be(id);
        diagnosis.CreatedAtUtc.Should().Be(Now);
        diagnosis.UpdatedAtUtc.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Failed_update_does_not_change_code_or_timestamp()
    {
        var diagnosis = DiagnosisDefinition.Create("A00.00", "Холера", Now);
        var act = () => diagnosis.Update("B01.00", " ", Now.AddHours(1));

        act.Should().Throw<DomainValidationException>();
        diagnosis.Code.Should().Be("A00.00");
        diagnosis.UpdatedAtUtc.Should().Be(Now);
    }
}

