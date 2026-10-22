using Microsoft.Extensions.Configuration;

namespace HospitalInformationSystem.Web.Configuration;

public sealed record UiMockOptions(bool Enabled)
{
    public static UiMockOptions FromConfiguration(IConfiguration configuration, string environment)
    {
        var enabled = configuration.GetValue<bool>("UiMock:Enabled");
        if (enabled && environment is not ("Development" or "UiMock"))
            throw new InvalidOperationException("UiMock доступен только в средах Development и UiMock.");
        if (!enabled && environment == "UiMock")
            throw new InvalidOperationException("Не найдена конфигурация UiMock. Используйте scripts/run-ui-mock.sh.");

        return new UiMockOptions(enabled);
    }
}
