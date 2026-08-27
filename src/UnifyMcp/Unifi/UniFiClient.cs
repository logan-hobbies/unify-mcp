using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using UnifyMcp.Configuration;
using UnifyMcp.Secrets;

namespace UnifyMcp.Unifi;

public sealed class UniFiClient : IAsyncDisposable
{
    private const string IntegrationPrefix = "/proxy/network/integration/v1";
    private const string ClassicPrefix = "/proxy/network/api/s";

    private static readonly string[] BlockedPathFragments = ["/actions", "/cmd/", "/rest/"];

    private static readonly Dictionary<string, string> ClassicReadPaths = new(StringComparer.Ordinal)
    {
        ["health"] = "stat/health",
        ["sysinfo"] = "stat/sysinfo",
        ["devices"] = "stat/device",
        ["devices_basic"] = "stat/device-basic",
        ["clients"] = "stat/sta",
        ["clients_all"] = "stat/alluser",
        ["events"] = "stat/event",
        ["alarms"] = "stat/alarm",
        ["anomalies"] = "stat/anomalies",
        ["rogue_aps"] = "stat/rogueap",
        ["gateway"] = "stat/gateway",
        ["dashboard"] = "stat/dashboard",
        ["site_dpi"] = "stat/sitedpi",
        ["client_dpi"] = "stat/stadpi",
        ["ips_events"] = "stat/ips/event",
        ["sessions"] = "stat/session",
        ["known_clients"] = "rest/user",
        ["networks"] = "rest/networkconf",
        ["wlans"] = "rest/wlanconf",
        ["firewall_rules"] = "rest/firewallrule",
        ["port_forwards"] = "rest/portforward",
        ["settings_ips"] = "rest/setting/ips",
    };

    private readonly AzureSecretStore _secrets;
    private readonly UnifiSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private HttpClient? _classicClient;
    private bool _classicLoggedIn;
    private string? _apiKey;
    private string? _baseUrl;

    public UniFiClient(AzureSecretStore secrets, IOptions<UnifiSettings> settings)
    {
        _secrets = secrets;
        _settings = settings.Value;
    }

    private async Task EnsureConfigAsync(CancellationToken ct)
    {
        _baseUrl ??= await _secrets.GetControllerUrlAsync(ct);
        _apiKey ??= await _secrets.GetUnifiApiKeyAsync(ct);
    }

    public async Task<JsonElement> IntegrationGetAsync(
        string path,
        Dictionary<string, string>? query = null,
        CancellationToken ct = default)
    {
        AssertGetOnly("GET");
        AssertReadOnlyPath(path);
        await EnsureConfigAsync(ct);

        using var client = CreateAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(IntegrationUrl(path), query));
        request.Headers.Add("X-API-KEY", _apiKey);

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response, ct);
    }

    public async Task<JsonElement> ClassicGetAsync(
        string path,
        Dictionary<string, string>? query = null,
        CancellationToken ct = default)
    {
        AssertGetOnly("GET");
        AssertReadOnlyPath(path);
        await EnsureConfigAsync(ct);

        var client = await EnsureClassicSessionAsync(ct);
        var relativePath = ClassicRelativePath(path);
        using var response = await client.GetAsync(BuildRelativeUri(relativePath, query), ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _classicLoggedIn = false;
            client = await EnsureClassicSessionAsync(ct);
            response.Dispose();
            using var retry = await client.GetAsync(BuildRelativeUri(relativePath, query), ct);
            retry.EnsureSuccessStatusCode();
            var retryPayload = await ReadJsonAsync(retry, ct);
            ValidateClassicPayload(retryPayload);
            return retryPayload;
        }

        response.EnsureSuccessStatusCode();
        var payload = await ReadJsonAsync(response, ct);
        ValidateClassicPayload(payload);
        return payload;
    }

    public async Task<JsonElement> ClassicV2GetAsync(
        string path,
        Dictionary<string, string>? query = null,
        CancellationToken ct = default)
    {
        AssertGetOnly("GET");
        AssertReadOnlyPath(path);
        await EnsureConfigAsync(ct);

        var client = await EnsureClassicSessionAsync(ct);
        var relativePath = $"/proxy/network/v2/api/site/{_settings.Site}/{path.TrimStart('/')}";
        using var response = await client.GetAsync(BuildRelativeUri(relativePath, query), ct);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response, ct);
    }

    public async Task<JsonElement> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var info = await IntegrationGetAsync("/info", ct: ct);
            return JsonSerializer.SerializeToElement(new { reachable = true, api = "integration", info }, _jsonOptions);
        }
        catch (Exception integrationError)
        {
            try
            {
                var health = await ClassicGetAsync("health", ct: ct);
                return JsonSerializer.SerializeToElement(new
                {
                    reachable = true,
                    api = "classic",
                    health,
                    integration_error = integrationError.Message,
                }, _jsonOptions);
            }
            catch (Exception classicError)
            {
                return JsonSerializer.SerializeToElement(new
                {
                    reachable = false,
                    integration_error = integrationError.Message,
                    classic_error = classicError.Message,
                }, _jsonOptions);
            }
        }
    }

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

    private async Task<HttpClient> EnsureClassicSessionAsync(CancellationToken ct)
    {
        if (_classicClient is not null && _classicLoggedIn)
        {
            return _classicClient;
        }

        await EnsureConfigAsync(ct);

        var credentials = await _secrets.GetClassicCredentialsAsync(ct)
            ?? throw new InvalidOperationException(
                "Classic UniFi diagnostics require unifi-username and unifi-password secrets " +
                "in Azure Key Vault. Create a View Only local admin for read-only access.");

        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            ServerCertificateCustomValidationCallback = _settings.VerifySsl
                ? null
                : HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

        _classicClient?.Dispose();
        _classicClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_baseUrl!),
            Timeout = TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds),
        };
        _classicClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var loginResponse = await _classicClient.PostAsJsonAsync(
            "/api/auth/login",
            new { username = credentials.Username, password = credentials.Password, remember = true },
            ct);

        loginResponse.EnsureSuccessStatusCode();

        if (loginResponse.Headers.TryGetValues("x-csrf-token", out var csrfValues)
            || loginResponse.Headers.TryGetValues("x-updated-csrf-token", out csrfValues))
        {
            _classicClient.DefaultRequestHeaders.Remove("X-CSRF-Token");
            _classicClient.DefaultRequestHeaders.Add("X-CSRF-Token", csrfValues.First());
        }

        _classicLoggedIn = true;
        return _classicClient;
    }

    private HttpClient CreateAnonymousClient() =>
        new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = _settings.VerifySsl
                ? null
                : HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        })
        {
            Timeout = TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds),
        };

    private string IntegrationUrl(string path)
    {
        var normalized = path.StartsWith('/') ? path : $"/{path}";
        if (normalized.StartsWith("/v1/", StringComparison.Ordinal))
        {
            normalized = normalized["/v1".Length..];
        }
        else if (normalized == "/v1")
        {
            normalized = "/";
        }

        return $"{_baseUrl}{IntegrationPrefix}{normalized}";
    }

    private string ClassicRelativePath(string path)
    {
        var normalized = path.TrimStart('/');
        var suffix = normalized.StartsWith("stat/", StringComparison.Ordinal)
            || normalized.StartsWith("rest/", StringComparison.Ordinal)
            || normalized.StartsWith("v2/", StringComparison.Ordinal)
            ? normalized
            : ClassicReadPaths.GetValueOrDefault(normalized, normalized);

        return $"{ClassicPrefix}/{_settings.Site}/{suffix}";
    }

    private static string BuildUri(string absoluteUrl, Dictionary<string, string>? query)
    {
        if (query is null || query.Count == 0)
        {
            return absoluteUrl;
        }

        var builder = new UriBuilder(absoluteUrl);
        builder.Query = string.Join("&", query.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return builder.Uri.ToString();
    }

    private static string BuildRelativeUri(string relativePath, Dictionary<string, string>? query)
    {
        if (query is null || query.Count == 0)
        {
            return relativePath;
        }

        var queryString = string.Join("&", query.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{relativePath}?{queryString}";
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return document.RootElement.Clone();
    }

    private static void AssertGetOnly(string method)
    {
        if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            throw new ReadOnlyViolationException($"Only GET requests are allowed; received {method.ToUpperInvariant()}.");
        }
    }

    private static void AssertReadOnlyPath(string path)
    {
        var lowered = path.ToLowerInvariant();
        foreach (var fragment in BlockedPathFragments)
        {
            if (lowered.Contains(fragment, StringComparison.Ordinal))
            {
                throw new ReadOnlyViolationException($"Blocked read-only violation: path contains '{fragment}'.");
            }
        }
    }

    private static void ValidateClassicPayload(JsonElement payload)
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

    public async ValueTask DisposeAsync()
    {
        if (_classicClient is not null)
        {
            _classicClient.Dispose();
            _classicClient = null;
            _classicLoggedIn = false;
        }

        await Task.CompletedTask;
    }
}
