using System.Text.Json;
using Refit;

namespace UnifyMcp.Unifi.Api;

[Headers("Accept: application/json")]
public interface IUniFiClassicApi
{
    [Get("/proxy/network/api/s/{site}/stat/health")]
    Task<JsonElement> GetHealthAsync(string site, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/sysinfo")]
    Task<JsonElement> GetSysinfoAsync(string site, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/device")]
    Task<JsonElement> ListDevicesAsync(string site, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/sta")]
    Task<JsonElement> ListClientsAsync(string site, [Query] int _limit = 100, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/event")]
    Task<JsonElement> GetEventsAsync(
        string site,
        [Query] int _limit = 100,
        [Query] string _sort = "-time",
        [Query] int? within = null,
        CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/alarm")]
    Task<JsonElement> GetAlarmsAsync(
        string site,
        [Query] int _limit = 100,
        [Query] string _sort = "-time",
        CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/anomalies")]
    Task<JsonElement> GetAnomaliesAsync(string site, [Query] int within = 24, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/rogueap")]
    Task<JsonElement> GetRogueApsAsync(string site, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/gateway")]
    Task<JsonElement> GetGatewayStatsAsync(string site, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/dashboard")]
    Task<JsonElement> GetDashboardAsync(string site, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/sitedpi")]
    Task<JsonElement> GetSiteDpiAsync(string site, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/stadpi")]
    Task<JsonElement> GetClientDpiAsync(string site, [Query] int _limit = 50, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/stat/ips/event")]
    Task<JsonElement> GetIpsEventsAsync(string site, [Query] int _limit = 100, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/rest/user")]
    Task<JsonElement> GetKnownClientsAsync(string site, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/rest/firewallrule")]
    Task<JsonElement> GetFirewallRulesAsync(string site, CancellationToken cancellationToken = default);

    [Get("/proxy/network/api/s/{site}/rest/setting/ips")]
    Task<JsonElement> GetIpsSettingsAsync(string site, CancellationToken cancellationToken = default);

    [Get("/proxy/network/v2/api/site/{site}/ports/port-anomalies")]
    Task<JsonElement> GetPortAnomaliesAsync(string site, CancellationToken cancellationToken = default);
}
