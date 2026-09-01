using System.Collections.Concurrent;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;
using UnifyMcp.Configuration;

namespace UnifyMcp.Secrets;

public sealed class AzureSecretStore
{
    private readonly SecretClient _client;
    private readonly AzureKeyVaultSettings _vaultSettings;
    private readonly UnifiSettings _unifiSettings;

    // Lazy<Task> so concurrent first-callers share one Key Vault round-trip per secret.
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> _cache = new(StringComparer.Ordinal);

    public AzureSecretStore(
        IOptions<AzureKeyVaultSettings> vaultSettings,
        IOptions<UnifiSettings> unifiSettings)
    {
        _vaultSettings = vaultSettings.Value;
        _unifiSettings = unifiSettings.Value;

        if (string.IsNullOrWhiteSpace(_vaultSettings.VaultUrl)
            || _vaultSettings.VaultUrl.Contains("your-vault", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "AzureKeyVault:VaultUrl is required (set AzureKeyVault__VaultUrl to your real vault URL).");
        }

        _client = new SecretClient(new Uri(_vaultSettings.VaultUrl), CreateCredential());
    }

    private TokenCredential CreateCredential()
    {
        if (!string.IsNullOrWhiteSpace(_vaultSettings.TenantId)
            && !string.IsNullOrWhiteSpace(_vaultSettings.ClientId)
            && !string.IsNullOrWhiteSpace(_vaultSettings.ClientSecret))
        {
            return new ClientSecretCredential(
                _vaultSettings.TenantId,
                _vaultSettings.ClientId,
                _vaultSettings.ClientSecret);
        }

        return new DefaultAzureCredential();
    }

    public async Task<string> GetSecretAsync(string name, bool required = true, CancellationToken ct = default)
    {
        var lazy = _cache.GetOrAdd(name, n => new Lazy<Task<string?>>(() => FetchAsync(n)));

        string? value;
        try
        {
            value = await lazy.Value.WaitAsync(ct);
        }
        catch
        {
            _cache.TryRemove(name, out _);
            throw;
        }

        if (value is null)
        {
            // Not found: don't cache so a secret added later is picked up without restart.
            _cache.TryRemove(name, out _);
            if (required)
            {
                throw new InvalidOperationException($"Secret '{name}' was not found in Key Vault.");
            }

            return string.Empty;
        }

        return value;
    }

    private async Task<string?> FetchAsync(string name)
    {
        try
        {
            var secret = await _client.GetSecretAsync(name);
            var value = secret.Value.Value;
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException($"Secret '{name}' exists but has no value.");
            }

            return value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public Task<string> GetUnifiApiKeyAsync(CancellationToken ct = default) =>
        GetSecretAsync(_vaultSettings.UnifiApiKeySecretName, required: true, ct);

    public async Task<string> GetControllerUrlAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_unifiSettings.ControllerUrl))
        {
            return _unifiSettings.ControllerUrl.TrimEnd('/');
        }

        var secretName = _vaultSettings.UnifiControllerUrlSecretName;
        if (!string.IsNullOrWhiteSpace(secretName))
        {
            var value = await GetSecretAsync(secretName, required: false, ct);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.TrimEnd('/');
            }
        }

        throw new InvalidOperationException(
            "UniFi controller URL not configured. Set Unifi:ControllerUrl or store " +
            $"'{secretName}' in Azure Key Vault.");
    }

    public async Task<(string Username, string Password)?> GetClassicCredentialsAsync(CancellationToken ct = default)
    {
        var usernameName = _vaultSettings.UnifiUsernameSecretName;
        var passwordName = _vaultSettings.UnifiPasswordSecretName;
        if (string.IsNullOrWhiteSpace(usernameName) || string.IsNullOrWhiteSpace(passwordName))
        {
            return null;
        }

        var username = await GetSecretAsync(usernameName, required: false, ct);
        var password = await GetSecretAsync(passwordName, required: false, ct);
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return (username, password);
    }
}
