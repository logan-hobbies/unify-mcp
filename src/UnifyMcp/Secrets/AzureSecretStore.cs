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
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);

    public AzureSecretStore(
        IOptions<AzureKeyVaultSettings> vaultSettings,
        IOptions<UnifiSettings> unifiSettings)
    {
        _vaultSettings = vaultSettings.Value;
        _unifiSettings = unifiSettings.Value;

        if (string.IsNullOrWhiteSpace(_vaultSettings.VaultUrl))
        {
            throw new InvalidOperationException("AzureKeyVault:VaultUrl is required.");
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
        if (_cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        try
        {
            var secret = await _client.GetSecretAsync(name, cancellationToken: ct);
            var value = secret.Value.Value;
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException($"Secret '{name}' exists but has no value.");
            }

            _cache[name] = value;
            return value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            if (required)
            {
                throw new InvalidOperationException($"Secret '{name}' was not found in Key Vault.", ex);
            }

            return string.Empty;
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
