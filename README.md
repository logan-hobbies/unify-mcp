# unify-mcp

Read-only [Model Context Protocol](https://modelcontextprotocol.io/) server that connects AI assistants to your **UniFi home network**. Secrets (API key, optional classic credentials) live in **Azure Key Vault**; the server is designed to run on a **VPS** and expose diagnostics over HTTP.

**Read-only by design:** every tool maps to UniFi GET endpoints. No creates, updates, restarts, or client actions.

## What you can ask AI to do

- Check site health (WAN/LAN/WLAN)
- List devices, clients, networks, WANs, firewall policies
- Pull **traffic / DPI** breakdowns
- Review **anomalies**, **alarms**, **IPS/IDS events**, rogue APs
- Search recent events for disconnects, blocks, etc.
- Run a bundled **`unifi_troubleshoot_summary`** for AI-assisted diagnosis

## Architecture

```
 AI client (Cursor) ──HTTP──► VPS (unify-mcp) ──X-API-KEY──► UniFi Integration API
                                    │
                                    └── Azure Key Vault (secrets)
                                    └── Cookie auth ──► UniFi Classic API (diagnostics)
```

| API | Auth | Used for |
|---|---|---|
| Integration `/proxy/network/integration/v1` | `X-API-KEY` from Key Vault | Sites, devices, clients, networks, WANs |
| Classic `/proxy/network/api/s/{site}` | View Only local admin in Key Vault | Anomalies, events, alarms, DPI, IPS, dashboard |

## Quick start

### 1. Store secrets in Azure Key Vault

```bash
az keyvault secret set --vault-name YOUR_VAULT --name unifi-api-key --value "YOUR_UNIFI_API_KEY"
az keyvault secret set --vault-name YOUR_VAULT --name unifi-controller-url --value "https://192.168.1.1"
az keyvault secret set --vault-name YOUR_VAULT --name unifi-username --value "mcp-readonly"
az keyvault secret set --vault-name YOUR_VAULT --name unifi-password --value "YOUR_PASSWORD"
```

Create the UniFi API key under **Settings → Control Plane → Integrations** using a **View Only** local admin.

See [deploy/azure-setup.md](deploy/azure-setup.md) for managed identity and remote access options (Site Manager connector, VPN).

### 2. Configure environment

```bash
cp .env.example .env
# Edit AZURE_KEY_VAULT_URL and other values
```

### 3. Run locally

```bash
python -m pip install -e ".[dev]"
unify-mcp
```

Health check: `GET http://localhost:8080/health`

### 4. Deploy on VPS (Docker)

```bash
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

Set `MCP_AUTH_TOKEN` in production. Terminate TLS at nginx/Caddy in front of the container.

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
| … | See `src/unify_mcp/server.py` for the full list |

## Development

```bash
python -m pip install -e ".[dev]"
pytest
ruff check src tests
```

## Security notes

- Use a **View Only** UniFi local admin for both the API key and classic credentials.
- Grant the VPS identity only **Key Vault Secrets User** on the vault.
- Prefer the **Site Manager connector** or VPN instead of exposing your controller to the internet.
- This server never exposes secret values through MCP tools.

## License

MIT
