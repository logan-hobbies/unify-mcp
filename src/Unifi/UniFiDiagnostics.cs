using System.Text.Json;
using System.Text.RegularExpressions;

namespace UnifyMcp.Unifi;

public static class UniFiDiagnostics
{
    public static JsonElement SummarizeHealth(JsonElement healthPayload)
    {
        if (healthPayload.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            var subsystems = new Dictionary<string, object?>();
            foreach (var item in data.EnumerateArray())
            {
                if (!item.TryGetProperty("subsystem", out var subsystemProp))
                {
                    continue;
                }

                var name = subsystemProp.GetString() ?? "unknown";
                subsystems[name] = new
                {
                    status = GetString(item, "status"),
                    num_user = GetInt(item, "num_user"),
                    num_guest = GetInt(item, "num_guest"),
                    num_ap = GetInt(item, "num_ap"),
                    wan_ip = GetString(item, "wan_ip"),
                    tx_bytes = GetLong(item, "tx_bytes"),
                    rx_bytes = GetLong(item, "rx_bytes"),
                    latency = GetLong(item, "latency"),
                };
            }

            return JsonSerializer.SerializeToElement(new { subsystems });
        }

        return healthPayload.Clone();
    }

    public static List<JsonElement> FilterEvents(
        IEnumerable<JsonElement> events,
        IReadOnlyList<string> keywords,
        int limit)
    {
        var materialized = events.ToList();
        if (keywords.Count == 0)
        {
            return materialized.Take(limit).Select(e => e.Clone()).ToList();
        }

        var pattern = new Regex(
            string.Join("|", keywords.Select(Regex.Escape)),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return materialized
            .Where(e =>
            {
                var msg = GetString(e, "msg") ?? string.Empty;
                var key = GetString(e, "key") ?? string.Empty;
                return pattern.IsMatch(msg) || pattern.IsMatch(key);
            })
            .Take(limit)
            .Select(e => e.Clone())
            .ToList();
    }

    public static void ValidateClassicPayload(JsonElement payload)
    {
        if (payload.TryGetProperty("meta", out var meta)
            && meta.TryGetProperty("rc", out var rc)
            && rc.GetString() == "error")
        {
            var message = meta.TryGetProperty("msg", out var msg) ? msg.GetString() : "UniFi classic API error";
            throw new InvalidOperationException(message);
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() : null;

    private static int? GetInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var number) ? number : null;

    private static long? GetLong(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt64(out var number) ? number : null;
}
