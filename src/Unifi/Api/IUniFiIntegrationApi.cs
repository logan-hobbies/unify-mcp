using System.Text.Json;
using Refit;

namespace UnifyMcp.Unifi.Api;

[Headers("Accept: application/json")]
public interface IUniFiIntegrationApi
{
    [Get("/proxy/network/integration/v1/info")]
    Task<JsonElement> GetInfoAsync(CancellationToken cancellationToken = default);

    [Get("/proxy/network/integration/v1/sites")]
    Task<JsonElement> ListSitesAsync(CancellationToken cancellationToken = default);

    [Get("/proxy/network/integration/v1/sites/{siteId}/devices")]
    Task<JsonElement> ListDevicesAsync(string siteId, CancellationToken cancellationToken = default);

    [Get("/proxy/network/integration/v1/sites/{siteId}/devices/{deviceId}")]
    Task<JsonElement> GetDeviceAsync(string siteId, string deviceId, CancellationToken cancellationToken = default);

    [Get("/proxy/network/integration/v1/sites/{siteId}/devices/{deviceId}/statistics/latest")]
    Task<JsonElement> GetDeviceStatsAsync(string siteId, string deviceId, CancellationToken cancellationToken = default);

    [Get("/proxy/network/integration/v1/sites/{siteId}/clients")]
    Task<JsonElement> ListClientsAsync(
        string siteId,
        [Query] int limit = 200,
        [Query] int offset = 0,
        CancellationToken cancellationToken = default);

    [Get("/proxy/network/integration/v1/sites/{siteId}/clients/{clientId}")]
    Task<JsonElement> GetClientAsync(string siteId, string clientId, CancellationToken cancellationToken = default);

    [Get("/proxy/network/integration/v1/sites/{siteId}/networks")]
    Task<JsonElement> ListNetworksAsync(string siteId, CancellationToken cancellationToken = default);

    [Get("/proxy/network/integration/v1/sites/{siteId}/wifi/broadcasts")]
    Task<JsonElement> ListWifiBroadcastsAsync(string siteId, CancellationToken cancellationToken = default);

    [Get("/proxy/network/integration/v1/sites/{siteId}/wans")]
    Task<JsonElement> ListWansAsync(string siteId, CancellationToken cancellationToken = default);

    [Get("/proxy/network/integration/v1/sites/{siteId}/firewall/policies")]
    Task<JsonElement> ListFirewallPoliciesAsync(string siteId, CancellationToken cancellationToken = default);

    [Get("/proxy/network/integration/v1/dpi/applications")]
    Task<JsonElement> ListDpiApplicationsAsync(CancellationToken cancellationToken = default);
}
