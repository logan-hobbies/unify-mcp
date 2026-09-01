using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Refit;
using UnifyMcp.Configuration;
using UnifyMcp.Secrets;
using UnifyMcp.Unifi.Api;

namespace UnifyMcp.Unifi;

public sealed class UniFiClient : IAsyncDisposable
{
    private readonly AzureSecretStore _secrets;
    private readonly UnifiSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions = UniFiRefitSettings.JsonOptions;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly RefitSettings _refitSettings = UniFiRefitSettings.Create();

    private HttpClient? _integrationHttp;
    private HttpClient? _classicHttp;
    private IUniFiIntegrationApi? _integration;
    private IUniFiClassicApi? _classic;
    private IUniFiAuthApi? _auth;
    private bool _classicLoggedIn;

    public UniFiClient(AzureSecretStore secrets, IOptions<UnifiSettings> settings)
    {
        _secrets = secrets;
        _settings = settings.Value;
    }

    public string Site => _settings.Site;

    public async Task<IUniFiIntegrationApi> IntegrationAsync(CancellationToken ct = default)
    {
        if (_integration is not null)
        {
            return _integration;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_integration is not null)
            {
                return _integration;
            }

            var baseUrl = await _secrets.GetControllerUrlAsync(ct);
            var handler = new UniFiApiKeyHandler(_secrets)
            {
                InnerHandler = CreateSocketsHandler(useCookies: false),
            };
            _integrationHttp = CreateHttpClient(baseUrl, handler);
            _integration = RestService.For<IUniFiIntegrationApi>(_integrationHttp, _refitSettings);
            return _integration;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IUniFiClassicApi> ClassicAsync(CancellationToken ct = default)
    {
        await EnsureClassicSessionAsync(ct);
        return _classic!;
    }

    public async Task<JsonElement> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var integration = await IntegrationAsync(ct);
            var info = await integration.GetInfoAsync(ct);
            return JsonSerializer.SerializeToElement(new { reachable = true, api = "integration", info }, _jsonOptions);
        }
        catch (Exception integrationError)
        {
            try
            {
                var classic = await ClassicAsync(ct);
                var health = await classic.GetHealthAsync(Site, ct);
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

    public async Task<T> CallClassicAsync<T>(Func<IUniFiClassicApi, Task<T>> call, CancellationToken ct = default)
    {
        var api = await ClassicAsync(ct);
        try
        {
            return await call(api);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            _classicLoggedIn = false;
            api = await ClassicAsync(ct);
            return await call(api);
        }
    }

    private async Task EnsureClassicSessionAsync(CancellationToken ct)
    {
        if (_classic is not null && _classicLoggedIn)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_classic is not null && _classicLoggedIn)
            {
                return;
            }

            var credentials = await _secrets.GetClassicCredentialsAsync(ct)
                ?? throw new InvalidOperationException(
                    "Classic UniFi diagnostics require unifi-username and unifi-password secrets " +
                    "in Azure Key Vault. Create a View Only local admin for read-only access.");

            if (_classicHttp is null)
            {
                var baseUrl = await _secrets.GetControllerUrlAsync(ct);
                _classicHttp = CreateHttpClient(baseUrl, CreateSocketsHandler(useCookies: true));
                _auth = RestService.For<IUniFiAuthApi>(_classicHttp, _refitSettings);
                _classic = RestService.For<IUniFiClassicApi>(_classicHttp, _refitSettings);
            }

            using var loginResponse = await _auth!.LoginAsync(
                new UniFiLoginRequest(credentials.Username, credentials.Password),
                ct);
            loginResponse.EnsureSuccessStatusCode();

            if (loginResponse.Headers.TryGetValues("x-csrf-token", out var csrfValues)
                || loginResponse.Headers.TryGetValues("x-updated-csrf-token", out csrfValues))
            {
                _classicHttp.DefaultRequestHeaders.Remove("X-CSRF-Token");
                _classicHttp.DefaultRequestHeaders.Add("X-CSRF-Token", csrfValues.First());
            }

            _classicLoggedIn = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private HttpMessageHandler CreateSocketsHandler(bool useCookies)
    {
        var handler = new SocketsHttpHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = useCookies,
            // Never follow redirects: an API key or session cookie must not be replayed to another host.
            AllowAutoRedirect = false,
            // Recycle connections so DNS/Tailscale IP changes are picked up on a long-lived client.
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        if (!_settings.VerifySsl)
        {
            // Home consoles ship self-signed certs; only relax validation for the UniFi host.
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
        }

        return handler;
    }

    private HttpClient CreateHttpClient(string baseUrl, HttpMessageHandler handler) =>
        new(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds),
        };

    public async ValueTask DisposeAsync()
    {
        _integrationHttp?.Dispose();
        _classicHttp?.Dispose();
        _integrationHttp = null;
        _classicHttp = null;
        _integration = null;
        _classic = null;
        _auth = null;
        _classicLoggedIn = false;
        _gate.Dispose();
        await Task.CompletedTask;
    }
}
