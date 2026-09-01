# Deploying unify-mcp behind Tailscale

Goal: Cursor (Grok or any model) on your PC → tailnet → VPS running unify-mcp → tailnet → home UniFi.
No public ports on the VPS. No inbound exposure of the UniFi console.

```
 PC (Cursor)           VPS (Docker)                      Home
 tailscale ──────────► tailscale sidecar ──────────────► UDM / subnet router
                       └─ unify-mcp :8080 (shared netns)   192.168.x.x
```

## 1. Tailnet prerequisites

1. Install Tailscale on the PC that runs Cursor.
2. Get UniFi reachable on the tailnet, one of:
   - Tailscale on the console itself (UDM Pro/SE/UCG with the Tailscale package or a container), or
   - A **subnet router** on an always-on home box: `tailscale up --advertise-routes=192.168.1.0/24`, then approve the route in the admin console.
3. In the Tailscale admin console create a **reusable, pre-authorized** auth key tagged `tag:mcp`.

Suggested ACL so only your devices can reach the MCP:

```json
{
  "tagOwners": { "tag:mcp": ["autogroup:admin"] },
  "acls": [
    { "action": "accept", "src": ["autogroup:member"], "dst": ["tag:mcp:8080"] },
    { "action": "accept", "src": ["tag:mcp"], "dst": ["192.168.1.0/24:443", "100.64.0.0/10:443"] }
  ]
}
```

## 2. VPS

```bash
git clone https://github.com/logan-hobbies/unify-mcp && cd unify-mcp
cp .env.example .env
# fill TS_AUTHKEY, AZURE_KEY_VAULT_URL, UNIFI_CONTROLLER_URL, MCP_AUTH_TOKEN
docker compose up -d --build
docker compose exec tailscale tailscale status      # confirm node "unify-mcp" is online
docker compose logs -f unify-mcp
```

The MCP container uses `network_mode: service:tailscale`, so `0.0.0.0:8080` inside that namespace
is only the Tailscale interface and loopback. Verify nothing is public:

```bash
ss -tlnp | grep 8080        # should show nothing on the host
curl -s http://<tailscale-ip>:8080/health   # from your PC, on the tailnet
```

Key Vault access from the VPS: either an Azure service principal in `.env`, or run the VPS in Azure
with a managed identity (see `azure-setup.md`).

## 3. UniFi side

- Create a **local** (not Ubiquiti SSO) admin with **View Only** role, e.g. `mcp-readonly`.
- Create an API key under **Settings → Control Plane → Integrations** as that admin.
- Store `unifi-api-key`, `unifi-username`, `unifi-password` in Key Vault.
- `UNIFI_CONTROLLER_URL` is the console's Tailscale IP (`https://100.x.x.x`) or its LAN IP via the
  subnet router (`https://192.168.1.1`). Self-signed cert is expected; keep `UNIFI_VERIFY_SSL=false`.

## 4. Cursor

```json
{
  "mcpServers": {
    "unify": {
      "url": "http://unify-mcp:8080/mcp",
      "headers": { "Authorization": "Bearer <MCP_AUTH_TOKEN>" }
    }
  }
}
```

`unify-mcp` resolves via MagicDNS; use the `100.x.x.x` IP if MagicDNS is off. Plain HTTP is fine
here — the tailnet is already WireGuard-encrypted end to end.

## 5. First check

Ask the model to run `unifi_ping`. Expected: `reachable: true, api: "integration"`. Then
`unifi_get_site_health` to confirm the classic login works.

## Notes

- Cloud-hosted agents (e.g. Cursor Cloud Agents) are **not** on your tailnet and cannot reach this
  server. Only clients running on a tailnet device can.
- Rotate `MCP_AUTH_TOKEN` by editing `.env` and `docker compose up -d`.
- To bind on a bare VPS without the sidecar, set `Mcp__Host` to the VPS's Tailscale IP.
