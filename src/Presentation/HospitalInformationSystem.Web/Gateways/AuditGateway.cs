using System.Globalization;
using HospitalInformationSystem.Contracts.Security;

namespace HospitalInformationSystem.Web.Gateways;

public sealed class AuditGateway(HttpClient http) : IAuditGateway
{
    public async Task<PagedResult<AuditEventDto>> ListAsync(AuditQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Page < 1) throw new ArgumentOutOfRangeException(nameof(query), "Номер страницы должен быть положительным.");
        if (query.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(query), "Размер страницы должен быть от 1 до 100.");
        if (query.From > query.To)
            throw new ArgumentException("Начало периода не может быть позже окончания.", nameof(query));

        var values = new List<string>
        {
            $"page={query.Page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={query.PageSize.ToString(CultureInfo.InvariantCulture)}"
        };
        if (query.EventType is { } eventType)
        {
            if (!Enum.IsDefined(eventType)) throw new ArgumentOutOfRangeException(nameof(query));
            values.Add($"eventType={Uri.EscapeDataString(eventType.ToString())}");
        }
        if (query.From is { } from) values.Add($"from={EncodeDate(from)}");
        if (query.To is { } to) values.Add($"to={EncodeDate(to)}");
        var page = await GatewayHttpTransport.SendAsync<PagedResult<AuditEventDto>>(http, HttpMethod.Get,
            $"api/audit?{string.Join('&', values)}", null, cancellationToken);
        if (page.Items.Any(item => item is null))
            throw new ApiClients.ApiException("Сервер вернул некорректный список событий аудита.");
        return page;
    }

    private static string EncodeDate(DateTimeOffset value) =>
        Uri.EscapeDataString(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
}
