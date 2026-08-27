# unify-mcp

Read-only [Model Context Protocol](https://modelcontextprotocol.io/) server that connects AI assistants to your **UniFi home network**. Built with **.NET 10** and the official **MCP C# SDK 2.x**. Secrets live in **Azure Key Vault**; the server runs on a **VPS** over stateless Streamable HTTP.

**Read-only by design:** every tool maps to UniFi GET endpoints. No creates, updates, restarts, or client actions.

## Project layout

```
src/          Application code (UnifyMcp web + MCP server)
test/         Unit tests (xUnit)
deploy/       Azure and systemd deployment notes
```

This is the standard layout for a single-service .NET repo: production code in `src/`, tests in `test/` (or `tests/` — both are common).

## Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 |
| MCP | [ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) 2.x (stateless HTTP) |
| Secrets | Azure Key Vault + `DefaultAzureCredential` |
| UniFi | Integration API (`X-API-KEY`) + Classic API (View Only local admin) |

## What you can ask AI to do

- Check site health (WAN/LAN/WLAN)
- List devices, clients, networks, WANs, firewall policies
- Pull **traffic / DPI** breakdowns
- Review **anomalies**, **alarms**, **IPS/IDS events**, rogue APs
- Search recent events for disconnects, blocks, etc.
- Run **`unifi_troubleshoot_summary`** for AI-assisted diagnosis

## Architecture

```
 AI client (Cursor) ──HTTP──► VPS (unify-mcp, .NET 10) ──X-API-KEY──► UniFi Integration API
                                    │
                                    └── Azure Key Vault (secrets)
                                    └── Cookie auth ──► UniFi Classic API (diagnostics)
```

## Quick start

### 1. Store secrets in Azure Key Vault

```bash
az keyvault secret set --vault-name YOUR_VAULT --name unifi-api-key --value "YOUR_UNIFI_API_KEY"
az keyvault secret set --vault-name YOUR_VAULT --name unifi-controller-url --value "https://192.168.1.1"
az keyvault secret set --vault-name YOUR_VAULT --name unifi-username --value "mcp-readonly"
az keyvault secret set --vault-name YOUR_VAULT --name unifi-password --value "YOUR_PASSWORD"
```

Create the UniFi API key under **Settings → Control Plane → Integrations** using a **View Only** local admin.

See [deploy/azure-setup.md](deploy/azure-setup.md) for managed identity and remote access (Site Manager connector, VPN).

### 2. Configure

Edit `src/appsettings.json` or set environment variables:

```bash
export AzureKeyVault__VaultUrl=https://YOUR_VAULT.vault.azure.net/
export Unifi__ControllerUrl=https://192.168.1.1
export Mcp__AuthToken=your-production-token
```

### 3. Run locally

```bash
dotnet restore
dotnet run --project src/UnifyMcp.csproj
```

Health check: `GET http://localhost:8080/health`

### 4. Deploy on VPS (Docker)

```bash
cp .env.example .env   # fill in values
docker compose up -d --build
```

## Cursor MCP config

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

Set `Mcp__AuthToken` in production. Terminate TLS at nginx/Caddy in front of the container.

## Tools (29 read-only)

| Tool | Purpose |
|---|---|
| `unifi_ping` | Controller reachability |
| `unifi_troubleshoot_summary` | Aggregated diagnostic snapshot |
| `unifi_get_anomalies` | Poor signal, retries, saturation |
| `unifi_get_ips_events` | IPS/IDS security events |
| `unifi_get_site_dpi` / `unifi_get_client_dpi` | Traffic by app/category |
| `unifi_get_events` / `unifi_search_events` | Event log search |
| `unifi_list_devices` / `unifi_list_clients` | Integration API inventory |

Full list in `src/Tools/UniFiTools.cs`.

## Development

```bash
dotnet build
dotnet test
```

## Security notes

- Use a **View Only** UniFi local admin for both the API key and classic credentials.
- Grant the VPS identity only **Key Vault Secrets User** on the vault.
- Prefer the **Site Manager connector** or VPN instead of exposing your controller to the internet.
- This server never exposes secret values through MCP tools.

## License

MIT
