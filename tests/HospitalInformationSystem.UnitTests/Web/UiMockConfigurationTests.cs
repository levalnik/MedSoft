using FluentAssertions;
using HospitalInformationSystem.Web.Configuration;
using Microsoft.Extensions.Configuration;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class UiMockConfigurationTests
{
    [Fact]
    public void Mock_is_disabled_without_an_explicit_setting()
    {
        var configuration = new ConfigurationBuilder().Build();

        UiMockOptions.FromConfiguration(configuration, "Development").Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("UiMock")]
    public void Explicit_mock_setting_is_allowed_only_for_development(string environment)
    {
        UiMockOptions.FromConfiguration(EnabledConfiguration(), environment).Enabled.Should().BeTrue();
    }

    [Fact]
    public void Missing_mock_configuration_cannot_silently_use_a_real_API()
    {
        var action = () => UiMockOptions.FromConfiguration(new ConfigurationBuilder().Build(), "UiMock");

        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Mock_cannot_be_enabled_for_a_deployed_environment(string environment)
    {
        var action = () => UiMockOptions.FromConfiguration(EnabledConfiguration(), environment);

        action.Should().Throw<InvalidOperationException>();
    }

    private static IConfiguration EnabledConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["UiMock:Enabled"] = "true" })
        .Build();
}
