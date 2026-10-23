using System.Text.Json;
using FluentAssertions;
using HospitalInformationSystem.Contracts.Security;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class SecurityFixtureTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    [Fact]
    public void Deserialize_SharedJsonExamples_ShouldMatchPageAndGatewayContracts()
    {
        var session = Read<AuthenticationSessionDto>("session-doctor.json");
        var accounts = Read<AccountDetailsDto[]>("accounts.json");
        var audit = Read<PagedResult<AuditEventDto>>("audit-page.json");
        var setup = Read<SetupChallengeDto>("setup-challenge.json");

        session.MedicalWorkerRole.Should().Be(MedicalWorkerRole.Doctor);
        accounts.Should().HaveCount(2).And.Contain(account => account.Id == session.Id);
        audit.Items.Should().Contain(item => item.Actor.Kind == AuditActorKind.Anonymous && item.Actor.UserId == null);
        audit.Items[0].Metadata["revokedRefreshTokenCount"].GetInt32().Should().Be(2);
        setup.AuthenticatorUri.Should().StartWith("otpauth://totp/UiMock:");
    }

    private static T Read<T>(string name) => JsonSerializer.Deserialize<T>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Security", name)), Options)!;
}
