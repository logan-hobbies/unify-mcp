# unify-mcp

Read-only [Model Context Protocol](https://modelcontextprotocol.io/) server that connects AI assistants to your **UniFi home network**. Built with **.NET 10** and the official **MCP C# SDK 2.x**. Secrets live in **Azure Key Vault**; the server runs in Docker on a **VPS behind Tailscale** with no public ports.

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
| UniFi HTTP | [Refit](https://github.com/reactiveui/refit) typed clients (GET-only + login) |

## What you can ask AI to do

- Check site health (WAN/LAN/WLAN)
- List devices, clients, networks, WANs, firewall policies
- Pull **traffic / DPI** breakdowns
- Review **anomalies**, **alarms**, **IPS/IDS events**, rogue APs
- Search recent events for disconnects, blocks, etc.
- Run **`unifi_troubleshoot_summary`** for AI-assisted diagnosis

## Architecture

```
 PC (Cursor) ──tailnet──► VPS: tailscale sidecar + unify-mcp ──tailnet──► Home UniFi console
                                    │
                                    └── Azure Key Vault (API key, View Only creds)
```

Both hops ride Tailscale (WireGuard). The MCP container shares the Tailscale sidecar's network
namespace, so `:8080` is reachable only from your tailnet. See [deploy/tailscale.md](deploy/tailscale.md).

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

Environment variables (or `src/appsettings.json`):

```bash
export AzureKeyVault__VaultUrl=https://YOUR_VAULT.vault.azure.net/
export Unifi__ControllerUrl=https://100.x.x.x      # console's Tailscale or LAN IP
export Mcp__AuthToken=$(openssl rand -base64 32)
```

`Mcp__AuthToken` is **required** for HTTP transport; the server refuses to start without it.
For local dev only, `Mcp__AllowAnonymous=true` skips the check (the `http` launch profile sets this).

### 3. Run locally

```bash
dotnet run --project src/UnifyMcp.csproj
```

Health check: `GET http://127.0.0.1:8080/health` — MCP endpoint: `POST /mcp`

### 4. Deploy on VPS behind Tailscale (Docker)

```bash
cp .env.example .env   # TS_AUTHKEY, AZURE_KEY_VAULT_URL, UNIFI_CONTROLLER_URL, MCP_AUTH_TOKEN
docker compose up -d --build
docker compose exec tailscale tailscale status
```

Full walkthrough incl. subnet router and ACLs: [deploy/tailscale.md](deploy/tailscale.md).

## Cursor MCP config

```json
{
  "mcpServers": {
    "unify": {
      "url": "http://unify-mcp:8080/mcp",
      "headers": {
        "Authorization": "Bearer YOUR_MCP_AUTH_TOKEN"
      }
    }
  }
}
```

`unify-mcp` is the MagicDNS name of the sidecar; use its `100.x.x.x` IP if MagicDNS is off. Plain
HTTP is fine because the tailnet is already encrypted. Cloud-hosted agents are not on your tailnet and
cannot reach this server.

## Tools (32 read-only)

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
- Keep the MCP on the tailnet; never publish `8080` on a public interface.
- Bearer auth is mandatory on HTTP and compared in constant time.
- Redirects are disabled on UniFi calls so the API key / session cookie can't be replayed elsewhere.
- Raw tools (WLANs, known clients, devices) can return SSIDs, hostnames, MACs and other network
  details; those go to the model. Review what you ask for.

## License

MIT
