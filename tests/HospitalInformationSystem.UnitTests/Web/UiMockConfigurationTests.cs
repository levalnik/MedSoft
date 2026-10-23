using FluentAssertions;
using HospitalInformationSystem.Web.Configuration;
using Microsoft.Extensions.Configuration;

namespace HospitalInformationSystem.UnitTests.Web;

public sealed class UiMockConfigurationTests
{
    [Fact]
    public void IsEnabled_MissingExplicitSetting_ShouldDisableMock()
    {
        var configuration = new ConfigurationBuilder().Build();

        UiMockOptions.FromConfiguration(configuration, "Development").Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("UiMock")]
    public void IsEnabled_ExplicitMockSetting_ShouldAllowDevelopmentEnvironment(string environment)
    {
        UiMockOptions.FromConfiguration(EnabledConfiguration(), environment).Enabled.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingMockConfiguration_ShouldRejectRealApiFallback()
    {
        var action = () => UiMockOptions.FromConfiguration(new ConfigurationBuilder().Build(), "UiMock");

        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void IsEnabled_DeployedEnvironment_ShouldRejectEnabledMock(string environment)
    {
        var action = () => UiMockOptions.FromConfiguration(EnabledConfiguration(), environment);

        action.Should().Throw<InvalidOperationException>();
    }

    private static IConfiguration EnabledConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["UiMock:Enabled"] = "true" })
        .Build();
}
