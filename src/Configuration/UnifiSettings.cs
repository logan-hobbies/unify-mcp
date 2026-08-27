namespace UnifyMcp.Configuration;

public sealed class AzureKeyVaultSettings
{
    public string VaultUrl { get; set; } = string.Empty;
    public string UnifiApiKeySecretName { get; set; } = "unifi-api-key";
    public string? UnifiControllerUrlSecretName { get; set; } = "unifi-controller-url";
    public string? UnifiUsernameSecretName { get; set; } = "unifi-username";
    public string? UnifiPasswordSecretName { get; set; } = "unifi-password";
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}

public sealed class UnifiSettings
{
    public string? ControllerUrl { get; set; }
    public string Site { get; set; } = "default";
    public bool VerifySsl { get; set; }
    public double RequestTimeoutSeconds { get; set; } = 30;
}

public sealed class McpSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8080;
    public string Transport { get; set; } = "streamable-http";
    public string? AuthToken { get; set; }
}
