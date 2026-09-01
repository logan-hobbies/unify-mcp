using System.Text.Json;
using UnifyMcp.Unifi.Api;

namespace UnifyMcp.Unifi;

public sealed class UniFiService(UniFiClient client)
{
    public UniFiClient Client => client;

    public async Task<JsonElement> GetAppInfoAsync(CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).GetInfoAsync(ct);

    public async Task<JsonElement> ListSitesAsync(CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).ListSitesAsync(ct);

    public async Task<JsonElement> ListDevicesAsync(string siteId, CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).ListDevicesAsync(siteId, ct);

    public async Task<JsonElement> GetDeviceAsync(string siteId, string deviceId, CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).GetDeviceAsync(siteId, deviceId, ct);

    public async Task<JsonElement> GetDeviceStatsAsync(string siteId, string deviceId, CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).GetDeviceStatsAsync(siteId, deviceId, ct);

    public async Task<JsonElement> ListClientsAsync(
        string siteId,
        int limit = 200,
        int offset = 0,
        CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).ListClientsAsync(siteId, limit, offset, ct);

    public async Task<JsonElement> GetClientAsync(string siteId, string clientId, CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).GetClientAsync(siteId, clientId, ct);

    public async Task<JsonElement> ListNetworksAsync(string siteId, CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).ListNetworksAsync(siteId, ct);

    public async Task<JsonElement> ListWifiBroadcastsAsync(string siteId, CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).ListWifiBroadcastsAsync(siteId, ct);

    public async Task<JsonElement> ListWansAsync(string siteId, CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).ListWansAsync(siteId, ct);

    public async Task<JsonElement> ListFirewallPoliciesAsync(string siteId, CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).ListFirewallPoliciesAsync(siteId, ct);

    public async Task<JsonElement> ListDpiApplicationsAsync(CancellationToken ct = default) =>
        await (await client.IntegrationAsync(ct)).ListDpiApplicationsAsync(ct);

    public async Task<JsonElement> GetSiteHealthAsync(CancellationToken ct = default)
    {
        var payload = await CallClassicAsync(api => api.GetHealthAsync(client.Site, ct), ct);
        return UniFiDiagnostics.SummarizeHealth(payload);
    }

    public Task<JsonElement> GetSysinfoAsync(CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetSysinfoAsync(client.Site, ct), ct);

    public Task<JsonElement> ListClassicDevicesAsync(CancellationToken ct = default) =>
        CallClassicAsync(api => api.ListDevicesAsync(client.Site, ct), ct);

    public Task<JsonElement> ListClassicClientsAsync(int limit = 100, CancellationToken ct = default) =>
        CallClassicAsync(api => api.ListClientsAsync(client.Site, limit, ct), ct);

    public Task<JsonElement> GetEventsAsync(int? withinHours = 24, int limit = 100, CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetEventsAsync(client.Site, limit, "-time", withinHours, ct), ct);

    public Task<JsonElement> GetAlarmsAsync(int limit = 100, CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetAlarmsAsync(client.Site, limit, "-time", ct), ct);

    public Task<JsonElement> GetAnomaliesAsync(int withinHours = 24, CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetAnomaliesAsync(client.Site, withinHours, ct), ct);

    public Task<JsonElement> GetRogueApsAsync(CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetRogueApsAsync(client.Site, ct), ct);

    public Task<JsonElement> GetGatewayStatsAsync(CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetGatewayStatsAsync(client.Site, ct), ct);

    public Task<JsonElement> GetDashboardAsync(CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetDashboardAsync(client.Site, ct), ct);

    public Task<JsonElement> GetSiteDpiAsync(CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetSiteDpiAsync(client.Site, ct), ct);

    public Task<JsonElement> GetClientDpiAsync(int limit = 50, CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetClientDpiAsync(client.Site, limit, ct), ct);

    public Task<JsonElement> GetIpsEventsAsync(int limit = 100, CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetIpsEventsAsync(client.Site, limit, ct), ct);

    public Task<JsonElement> GetPortAnomaliesAsync(CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetPortAnomaliesAsync(client.Site, ct), ct);

    public Task<JsonElement> GetKnownClientsAsync(CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetKnownClientsAsync(client.Site, ct), ct);

    public Task<JsonElement> GetFirewallRulesAsync(CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetFirewallRulesAsync(client.Site, ct), ct);

    public Task<JsonElement> GetIpsSettingsAsync(CancellationToken ct = default) =>
        CallClassicAsync(api => api.GetIpsSettingsAsync(client.Site, ct), ct);

    public async Task<JsonElement> SearchEventsAsync(
        IReadOnlyList<string> keywords,
        int limit = 50,
        CancellationToken ct = default)
    {
        var payload = await GetEventsAsync(withinHours: 24, limit: Math.Max(limit, 200), ct);
        var events = ExtractDataArray(payload);
        var matches = UniFiDiagnostics.FilterEvents(events, keywords, limit);
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

        await CollectSignalAsync("anomalies", () => GetAnomaliesAsync(ct: ct), issues, signals);
        await CollectSignalAsync("alarms", () => GetAlarmsAsync(limit: 25, ct: ct), issues, signals);
        await CollectSignalAsync("ips_events", () => GetIpsEventsAsync(limit: 25, ct: ct), issues, signals);
        await CollectSignalAsync("rogue_aps", () => GetRogueApsAsync(ct), issues, signals);
        await CollectSignalAsync("gateway", () => GetGatewayStatsAsync(ct), issues, signals);
        await CollectSignalAsync("dashboard", () => GetDashboardAsync(ct), issues, signals);

        return JsonSerializer.SerializeToElement(summary);
    }

    private async Task<JsonElement> CallClassicAsync(
        Func<IUniFiClassicApi, Task<JsonElement>> call,
        CancellationToken ct)
    {
        var payload = await client.CallClassicAsync(call, ct);
        UniFiDiagnostics.ValidateClassicPayload(payload);
        return payload;
    }

    private static async Task CollectSignalAsync(
        string name,
        Func<Task<JsonElement>> fetch,
        List<string> issues,
        Dictionary<string, object?> signals)
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
