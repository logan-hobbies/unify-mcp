using System.Text.Json;
using UnifyMcp.Unifi;

namespace UnifyMcp.Unifi;

public sealed class UniFiService(UniFiClient client)
{
    public UniFiClient Client => client;

    public Task<JsonElement> GetAppInfoAsync(CancellationToken ct = default) =>
        client.IntegrationGetAsync("/info", ct: ct);

    public Task<JsonElement> ListSitesAsync(CancellationToken ct = default) =>
        client.IntegrationGetAsync("/sites", ct: ct);

    public Task<JsonElement> ListDevicesAsync(string siteId, CancellationToken ct = default) =>
        client.IntegrationGetAsync($"/sites/{siteId}/devices", ct: ct);

    public Task<JsonElement> GetDeviceAsync(string siteId, string deviceId, CancellationToken ct = default) =>
        client.IntegrationGetAsync($"/sites/{siteId}/devices/{deviceId}", ct: ct);

    public Task<JsonElement> GetDeviceStatsAsync(string siteId, string deviceId, CancellationToken ct = default) =>
        client.IntegrationGetAsync($"/sites/{siteId}/devices/{deviceId}/statistics/latest", ct: ct);

    public Task<JsonElement> ListClientsAsync(
        string siteId,
        int limit = 200,
        int offset = 0,
        CancellationToken ct = default) =>
        client.IntegrationGetAsync(
            $"/sites/{siteId}/clients",
            new Dictionary<string, string>
            {
                ["limit"] = limit.ToString(),
                ["offset"] = offset.ToString(),
            },
            ct);

    public Task<JsonElement> GetClientAsync(string siteId, string clientId, CancellationToken ct = default) =>
        client.IntegrationGetAsync($"/sites/{siteId}/clients/{clientId}", ct: ct);

    public Task<JsonElement> ListNetworksAsync(string siteId, CancellationToken ct = default) =>
        client.IntegrationGetAsync($"/sites/{siteId}/networks", ct: ct);

    public Task<JsonElement> ListWifiBroadcastsAsync(string siteId, CancellationToken ct = default) =>
        client.IntegrationGetAsync($"/sites/{siteId}/wifi/broadcasts", ct: ct);

    public Task<JsonElement> ListWansAsync(string siteId, CancellationToken ct = default) =>
        client.IntegrationGetAsync($"/sites/{siteId}/wans", ct: ct);

    public Task<JsonElement> ListFirewallPoliciesAsync(string siteId, CancellationToken ct = default) =>
        client.IntegrationGetAsync($"/sites/{siteId}/firewall/policies", ct: ct);

    public Task<JsonElement> ListDpiApplicationsAsync(CancellationToken ct = default) =>
        client.IntegrationGetAsync("/dpi/applications", ct: ct);

    public async Task<JsonElement> GetSiteHealthAsync(CancellationToken ct = default)
    {
        var payload = await client.ClassicGetAsync("health", ct: ct);
        return UniFiClient.SummarizeHealth(payload);
    }

    public Task<JsonElement> GetSysinfoAsync(CancellationToken ct = default) =>
        client.ClassicGetAsync("sysinfo", ct: ct);

    public Task<JsonElement> ListClassicDevicesAsync(CancellationToken ct = default) =>
        client.ClassicGetAsync("devices", ct: ct);

    public Task<JsonElement> ListClassicClientsAsync(int limit = 100, CancellationToken ct = default) =>
        client.ClassicGetAsync("clients", new Dictionary<string, string> { ["_limit"] = limit.ToString() }, ct);

    public Task<JsonElement> GetEventsAsync(int? withinHours = 24, int limit = 100, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string>
        {
            ["_limit"] = limit.ToString(),
            ["_sort"] = "-time",
        };
        if (withinHours is not null)
        {
            query["within"] = withinHours.Value.ToString();
        }

        return client.ClassicGetAsync("events", query, ct);
    }

    public Task<JsonElement> GetAlarmsAsync(int limit = 100, CancellationToken ct = default) =>
        client.ClassicGetAsync(
            "alarms",
            new Dictionary<string, string> { ["_limit"] = limit.ToString(), ["_sort"] = "-time" },
            ct);

    public Task<JsonElement> GetAnomaliesAsync(int withinHours = 24, CancellationToken ct = default) =>
        client.ClassicGetAsync("anomalies", new Dictionary<string, string> { ["within"] = withinHours.ToString() }, ct);

    public Task<JsonElement> GetRogueApsAsync(CancellationToken ct = default) =>
        client.ClassicGetAsync("rogue_aps", ct: ct);

    public Task<JsonElement> GetGatewayStatsAsync(CancellationToken ct = default) =>
        client.ClassicGetAsync("gateway", ct: ct);

    public Task<JsonElement> GetDashboardAsync(CancellationToken ct = default) =>
        client.ClassicGetAsync("dashboard", ct: ct);

    public Task<JsonElement> GetSiteDpiAsync(CancellationToken ct = default) =>
        client.ClassicGetAsync("site_dpi", ct: ct);

    public Task<JsonElement> GetClientDpiAsync(int limit = 50, CancellationToken ct = default) =>
        client.ClassicGetAsync("client_dpi", new Dictionary<string, string> { ["_limit"] = limit.ToString() }, ct);

    public Task<JsonElement> GetIpsEventsAsync(int limit = 100, CancellationToken ct = default) =>
        client.ClassicGetAsync("ips_events", new Dictionary<string, string> { ["_limit"] = limit.ToString() }, ct);

    public Task<JsonElement> GetPortAnomaliesAsync(CancellationToken ct = default) =>
        client.ClassicV2GetAsync("ports/port-anomalies", ct: ct);

    public Task<JsonElement> GetKnownClientsAsync(CancellationToken ct = default) =>
        client.ClassicGetAsync("known_clients", ct: ct);

    public Task<JsonElement> GetFirewallRulesAsync(CancellationToken ct = default) =>
        client.ClassicGetAsync("firewall_rules", ct: ct);

    public Task<JsonElement> GetIpsSettingsAsync(CancellationToken ct = default) =>
        client.ClassicGetAsync("settings_ips", ct: ct);

    public async Task<JsonElement> SearchEventsAsync(
        IReadOnlyList<string> keywords,
        int limit = 50,
        CancellationToken ct = default)
    {
        var payload = await GetEventsAsync(withinHours: 24, limit: Math.Max(limit, 200), ct);
        var events = ExtractDataArray(payload);
        var matches = UniFiClient.FilterEvents(events, keywords, limit);
        return JsonSerializer.SerializeToElement(new
        {
            keywords,
            count = matches.Count,
            matches,
        });
    }

    public async Task<JsonElement> BuildTroubleshootSummaryAsync(CancellationToken ct = default)
    {
        var summary = new Dictionary<string, object?>
        {
            ["issues"] = new List<string>(),
            ["signals"] = new Dictionary<string, object?>(),
        };

        var issues = (List<string>)summary["issues"]!;
        var signals = (Dictionary<string, object?>)summary["signals"]!;

        try
        {
            summary["health"] = await GetSiteHealthAsync(ct);
        }
        catch (Exception ex)
        {
            issues.Add($"health: {ex.Message}");
        }

        await CollectSignalAsync("anomalies", () => GetAnomaliesAsync(ct: ct), issues, signals, ct);
        await CollectSignalAsync("alarms", () => GetAlarmsAsync(limit: 25, ct: ct), issues, signals, ct);
        await CollectSignalAsync("ips_events", () => GetIpsEventsAsync(limit: 25, ct: ct), issues, signals, ct);
        await CollectSignalAsync("rogue_aps", () => GetRogueApsAsync(ct), issues, signals, ct);
        await CollectSignalAsync("gateway", () => GetGatewayStatsAsync(ct), issues, signals, ct);
        await CollectSignalAsync("dashboard", () => GetDashboardAsync(ct), issues, signals, ct);

        return JsonSerializer.SerializeToElement(summary);
    }

    private static async Task CollectSignalAsync(
        string name,
        Func<Task<JsonElement>> fetch,
        List<string> issues,
        Dictionary<string, object?> signals,
        CancellationToken ct)
    {
        try
        {
            var payload = await fetch();
            var data = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("data", out var inner)
                ? inner
                : payload;

            signals[name] = data;
            if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
            {
                issues.Add($"{name}: {data.GetArrayLength()} recent item(s)");
            }
        }
        catch (Exception ex)
        {
            signals[name] = new { error = ex.Message };
        }
    }

    private static IEnumerable<JsonElement> ExtractDataArray(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array)
        {
            return data.EnumerateArray();
        }

        if (payload.ValueKind == JsonValueKind.Array)
        {
            return payload.EnumerateArray();
        }

        return [];
    }
}
