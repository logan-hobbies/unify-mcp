# Azure setup for unify-mcp

## 1. Key Vault secrets

Create these secrets in Azure Key Vault:

| Secret name | Example | Required |
|---|---|---|
| `unifi-api-key` | UniFi Integrations API key | Yes |
| `unifi-controller-url` | `https://192.168.1.1` or Site Manager connector URL | Yes* |
| `unifi-username` | View Only local admin | For classic diagnostics |
| `unifi-password` | Local admin password | For classic diagnostics |

\* You can set `UNIFI_CONTROLLER_URL` in env instead of storing the URL in Key Vault
(typically the console's Tailscale IP, e.g. `https://100.x.x.x`).

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

Use **Tailscale** — see [tailscale.md](tailscale.md). The VPS reaches the console over the tailnet
(console on Tailscale, or a subnet router advertising your LAN), and Cursor reaches the MCP the same way.
Nothing is exposed to the public internet.

## 4. MCP client configuration

See [tailscale.md](tailscale.md#4-cursor).
