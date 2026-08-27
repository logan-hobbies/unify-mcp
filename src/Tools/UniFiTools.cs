using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using UnifyMcp.Unifi;

namespace UnifyMcp.Tools;

[McpServerToolType]
public sealed class UniFiTools(UniFiService service)
{
    private static string Format(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });

    [McpServerTool(Name = "unifi_ping"), Description("Check reachability of the UniFi controller.")]
    public async Task<string> PingAsync(CancellationToken cancellationToken) =>
        Format(await service.Client.PingAsync(cancellationToken));

    [McpServerTool(Name = "unifi_get_app_info"), Description("Get UniFi Network application version and metadata.")]
    public async Task<string> GetAppInfoAsync(CancellationToken cancellationToken) =>
        Format(await service.GetAppInfoAsync(cancellationToken));

    [McpServerTool(Name = "unifi_list_sites"), Description("List UniFi sites (Integration API, UUID site IDs).")]
    public async Task<string> ListSitesAsync(CancellationToken cancellationToken) =>
        Format(await service.ListSitesAsync(cancellationToken));

    [McpServerTool(Name = "unifi_list_devices"), Description("List adopted devices for a site.")]
    public async Task<string> ListDevicesAsync(
        [Description("UniFi site UUID from unifi_list_sites")] string site_id,
        CancellationToken cancellationToken) =>
        Format(await service.ListDevicesAsync(site_id, cancellationToken));

    [McpServerTool(Name = "unifi_get_device"), Description("Get one adopted device by ID.")]
    public async Task<string> GetDeviceAsync(string site_id, string device_id, CancellationToken cancellationToken) =>
        Format(await service.GetDeviceAsync(site_id, device_id, cancellationToken));

    [McpServerTool(Name = "unifi_get_device_stats"), Description("Latest CPU, memory, uptime, and radio stats.")]
    public async Task<string> GetDeviceStatsAsync(string site_id, string device_id, CancellationToken cancellationToken) =>
        Format(await service.GetDeviceStatsAsync(site_id, device_id, cancellationToken));

    [McpServerTool(Name = "unifi_list_clients"), Description("List connected clients for a site.")]
    public async Task<string> ListClientsAsync(
        string site_id,
        [Description("Maximum clients to return")] int limit = 200,
        int offset = 0,
        CancellationToken cancellationToken = default) =>
        Format(await service.ListClientsAsync(site_id, limit, offset, cancellationToken));

    [McpServerTool(Name = "unifi_get_client"), Description("Get one connected client by ID.")]
    public async Task<string> GetClientAsync(string site_id, string client_id, CancellationToken cancellationToken) =>
        Format(await service.GetClientAsync(site_id, client_id, cancellationToken));

    [McpServerTool(Name = "unifi_list_networks"), Description("List VLAN/LAN network definitions.")]
    public async Task<string> ListNetworksAsync(string site_id, CancellationToken cancellationToken) =>
        Format(await service.ListNetworksAsync(site_id, cancellationToken));

    [McpServerTool(Name = "unifi_list_wifi_broadcasts"), Description("List SSID / WiFi broadcast configurations.")]
    public async Task<string> ListWifiBroadcastsAsync(string site_id, CancellationToken cancellationToken) =>
        Format(await service.ListWifiBroadcastsAsync(site_id, cancellationToken));

    [McpServerTool(Name = "unifi_list_wans"), Description("List WAN interface configuration and status.")]
    public async Task<string> ListWansAsync(string site_id, CancellationToken cancellationToken) =>
        Format(await service.ListWansAsync(site_id, cancellationToken));

    [McpServerTool(Name = "unifi_list_firewall_policies"), Description("List firewall policies (Network 10.x+).")]
    public async Task<string> ListFirewallPoliciesAsync(string site_id, CancellationToken cancellationToken) =>
        Format(await service.ListFirewallPoliciesAsync(site_id, cancellationToken));

    [McpServerTool(Name = "unifi_list_dpi_applications"), Description("List DPI application catalog.")]
    public async Task<string> ListDpiApplicationsAsync(CancellationToken cancellationToken) =>
        Format(await service.ListDpiApplicationsAsync(cancellationToken));

    [McpServerTool(Name = "unifi_get_site_health"), Description("WAN/LAN/WLAN subsystem health summary.")]
    public async Task<string> GetSiteHealthAsync(CancellationToken cancellationToken) =>
        Format(await service.GetSiteHealthAsync(cancellationToken));

    [McpServerTool(Name = "unifi_get_sysinfo"), Description("Controller version, uptime, and environment info.")]
    public async Task<string> GetSysinfoAsync(CancellationToken cancellationToken) =>
        Format(await service.GetSysinfoAsync(cancellationToken));

    [McpServerTool(Name = "unifi_list_classic_devices"), Description("Full device list with radio and port stats.")]
    public async Task<string> ListClassicDevicesAsync(CancellationToken cancellationToken) =>
        Format(await service.ListClassicDevicesAsync(cancellationToken));

    [McpServerTool(Name = "unifi_list_classic_clients"), Description("Active clients with signal and throughput.")]
    public async Task<string> ListClassicClientsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        Format(await service.ListClassicClientsAsync(limit, cancellationToken));

    [McpServerTool(Name = "unifi_get_events"), Description("Recent UniFi events, newest first.")]
    public async Task<string> GetEventsAsync(int within_hours = 24, int limit = 100, CancellationToken cancellationToken = default) =>
        Format(await service.GetEventsAsync(within_hours, limit, cancellationToken));

    [McpServerTool(Name = "unifi_get_alarms"), Description("Active and recent alarms.")]
    public async Task<string> GetAlarmsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        Format(await service.GetAlarmsAsync(limit, cancellationToken));

    [McpServerTool(Name = "unifi_get_anomalies"), Description("Diagnostic anomalies such as poor signal or high retries.")]
    public async Task<string> GetAnomaliesAsync(int within_hours = 24, CancellationToken cancellationToken = default) =>
        Format(await service.GetAnomaliesAsync(within_hours, cancellationToken));

    [McpServerTool(Name = "unifi_get_rogue_aps"), Description("Nearby rogue APs detected by UniFi APs.")]
    public async Task<string> GetRogueApsAsync(CancellationToken cancellationToken) =>
        Format(await service.GetRogueApsAsync(cancellationToken));

    [McpServerTool(Name = "unifi_get_gateway_stats"), Description("Gateway WAN stats including speedtest results.")]
    public async Task<string> GetGatewayStatsAsync(CancellationToken cancellationToken) =>
        Format(await service.GetGatewayStatsAsync(cancellationToken));

    [McpServerTool(Name = "unifi_get_dashboard"), Description("Aggregated WAN throughput, latency, retries, and drops.")]
    public async Task<string> GetDashboardAsync(CancellationToken cancellationToken) =>
        Format(await service.GetDashboardAsync(cancellationToken));

    [McpServerTool(Name = "unifi_get_site_dpi"), Description("Site-wide traffic breakdown by app/category.")]
    public async Task<string> GetSiteDpiAsync(CancellationToken cancellationToken) =>
        Format(await service.GetSiteDpiAsync(cancellationToken));

    [McpServerTool(Name = "unifi_get_client_dpi"), Description("Per-client DPI traffic breakdown.")]
    public async Task<string> GetClientDpiAsync(int limit = 50, CancellationToken cancellationToken = default) =>
        Format(await service.GetClientDpiAsync(limit, cancellationToken));

    [McpServerTool(Name = "unifi_get_ips_events"), Description("IPS/IDS security events.")]
    public async Task<string> GetIpsEventsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        Format(await service.GetIpsEventsAsync(limit, cancellationToken));

    [McpServerTool(Name = "unifi_get_port_anomalies"), Description("Switch port anomalies such as errors or flapping.")]
    public async Task<string> GetPortAnomaliesAsync(CancellationToken cancellationToken) =>
        Format(await service.GetPortAnomaliesAsync(cancellationToken));

    [McpServerTool(Name = "unifi_get_known_clients"), Description("Configured client aliases and fixed-IP mappings.")]
    public async Task<string> GetKnownClientsAsync(CancellationToken cancellationToken) =>
        Format(await service.GetKnownClientsAsync(cancellationToken));

    [McpServerTool(Name = "unifi_get_firewall_rules"), Description("Legacy firewall rules list.")]
    public async Task<string> GetFirewallRulesAsync(CancellationToken cancellationToken) =>
        Format(await service.GetFirewallRulesAsync(cancellationToken));

    [McpServerTool(Name = "unifi_get_ips_settings"), Description("IPS/IDS configuration and status.")]
    public async Task<string> GetIpsSettingsAsync(CancellationToken cancellationToken) =>
        Format(await service.GetIpsSettingsAsync(cancellationToken));

    [McpServerTool(Name = "unifi_search_events"), Description("Filter recent events by keywords.")]
    public async Task<string> SearchEventsAsync(
        [Description("Keywords such as disconnect, blocked, rogue")] string[] keywords,
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        Format(await service.SearchEventsAsync(keywords, limit, cancellationToken));

    [McpServerTool(Name = "unifi_troubleshoot_summary"), Description("Aggregate health, anomalies, alarms, and security signals.")]
    public async Task<string> TroubleshootSummaryAsync(CancellationToken cancellationToken) =>
        Format(await service.BuildTroubleshootSummaryAsync(cancellationToken));
}
