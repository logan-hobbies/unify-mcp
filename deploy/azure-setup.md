# Azure setup for unify-mcp

## 1. Key Vault secrets

Create these secrets in Azure Key Vault:

| Secret name | Example | Required |
|---|---|---|
| `unifi-api-key` | UniFi Integrations API key | Yes |
| `unifi-controller-url` | `https://192.168.1.1` or Site Manager connector URL | Yes* |
| `unifi-username` | View Only local admin | For classic diagnostics |
| `unifi-password` | Local admin password | For classic diagnostics |

\* You can set `UNIFI_CONTROLLER_URL` in env instead of storing the URL in Key Vault.

### UniFi API key

1. UniFi Network → **Settings → Control Plane → Integrations**
2. **Create API Key** from a **View Only** local admin account
3. Store the key in Key Vault as `unifi-api-key`

### Classic diagnostics credentials

Anomalies, events, alarms, DPI, IPS events, and dashboard stats use the classic
read-only API and require a **local** View Only account (not Ubiquiti SSO):

```bash
az keyvault secret set --vault-name YOUR_VAULT --name unifi-username --value "mcp-readonly"
az keyvault secret set --vault-name YOUR_VAULT --name unifi-password --value "YOUR_PASSWORD"
```

## 2. Managed identity (recommended on Azure VM)

```bash
# Enable system-assigned identity on the VM
az vm identity assign -g YOUR_RG -n YOUR_VM

# Grant least-privilege secret read
PRINCIPAL_ID=$(az vm show -g YOUR_RG -n YOUR_VM --query identity.principalId -o tsv)
az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role "Key Vault Secrets User" \
  --scope "/subscriptions/SUB/resourceGroups/RG/providers/Microsoft.KeyVault/vaults/YOUR_VAULT"
```

Set on the VM (environment variables for ASP.NET Core):

```bash
AzureKeyVault__VaultUrl=https://YOUR_VAULT.vault.azure.net/
Unifi__ControllerUrl=https://YOUR_UNIFI_IP_OR_CONNECTOR_URL
```

## 3. Remote access to home UniFi

Pick one:

**A. UniFi Site Manager Connector (recommended)**

If your console firmware is ≥ 5.0.3, use the cloud connector so the VPS never needs
LAN access:

```
UNIFI_CONTROLLER_URL=https://api.ui.com/v1/connector/consoles/YOUR_CONSOLE_ID
```

**B. VPN / Tailscale**

Run the MCP on a host that can reach the UniFi gateway over VPN.

**C. Reverse proxy + IP allowlist**

Only if you accept exposing the controller; not recommended.

## 4. MCP client configuration

Point Cursor (or another MCP client) at the VPS:

```json
{
  "mcpServers": {
    "unify": {
      "url": "https://your-vps.example.com/mcp",
      "headers": {
        "Authorization": "Bearer YOUR_MCP_AUTH_TOKEN"
      }
    }
  }
}
```

Set `Mcp__AuthToken` in production. Terminate TLS at nginx/Caddy on the VPS.
