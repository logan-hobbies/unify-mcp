from __future__ import annotations

import asyncio
from typing import Any

from mcp.server.mcpserver import MCPServer

from unify_mcp import __version__
from unify_mcp.auth import StaticTokenVerifier
from unify_mcp.config import get_settings
from unify_mcp.context import get_service, lifespan_service

INSTRUCTIONS = """
Read-only UniFi home network MCP server.

Use these tools to inspect network health, devices, clients, traffic (DPI),
anomalies, IPS/security events, and alarms. All tools are GET-only — nothing
here can change UniFi configuration or client state.

Tips:
- Start with `unifi_ping` or `unifi_troubleshoot_summary` for a quick health pass.
- Integration tools (UUID site IDs) use the official X-API-KEY API.
- Classic diagnostic tools (anomalies, events, DPI) require View Only local
  admin credentials stored in Azure Key Vault as unifi-username / unifi-password.
- Default classic site slug is 'default' unless UNIFI_SITE is set.
""".strip()

_settings = get_settings()
_token_verifier = None
if _settings.mcp_auth_token is not None:
    _token_verifier = StaticTokenVerifier(_settings.mcp_auth_token.get_secret_value())

server = MCPServer(
    name="unify-mcp",
    title="UniFi Network (Read-Only)",
    description="Read-only MCP interface to a UniFi home network via Azure Key Vault secrets.",
    instructions=INSTRUCTIONS,
    version=__version__,
    lifespan=lifespan_service,
    token_verifier=_token_verifier,
)


@server.tool(
    title="Ping UniFi Controller",
    description="Check reachability of the UniFi controller via Integration and/or Classic API.",
)
async def unifi_ping() -> dict[str, Any]:
    service = get_service()
    return await service.client.ping()


@server.tool(
    title="Application Info",
    description="Get UniFi Network application version and integration API metadata.",
)
async def unifi_get_app_info() -> Any:
    return await get_service().get_app_info()


@server.tool(
    title="List Sites",
    description="List UniFi sites from the Integration API (returns UUID site IDs).",
)
async def unifi_list_sites() -> Any:
    return await get_service().list_sites()


@server.tool(
    title="List Devices",
    description="List adopted devices for a site (Integration API).",
)
async def unifi_list_devices(site_id: str) -> Any:
    return await get_service().list_devices(site_id)


@server.tool(
    title="Get Device",
    description="Get details for one adopted device (Integration API).",
)
async def unifi_get_device(site_id: str, device_id: str) -> Any:
    return await get_service().get_device(site_id, device_id)


@server.tool(
    title="Get Device Statistics",
    description="Latest CPU, memory, uptime, and radio stats for a device (Integration API).",
)
async def unifi_get_device_stats(site_id: str, device_id: str) -> Any:
    return await get_service().get_device_stats(site_id, device_id)


@server.tool(
    title="List Clients",
    description="List connected clients for a site (Integration API).",
)
async def unifi_list_clients(site_id: str, limit: int = 200, offset: int = 0) -> Any:
    return await get_service().list_clients(site_id, limit=limit, offset=offset)


@server.tool(
    title="Get Client",
    description="Get details for one connected client (Integration API).",
)
async def unifi_get_client(site_id: str, client_id: str) -> Any:
    return await get_service().get_client(site_id, client_id)


@server.tool(
    title="List Networks",
    description="List VLAN/LAN network definitions (Integration API).",
)
async def unifi_list_networks(site_id: str) -> Any:
    return await get_service().list_networks(site_id)


@server.tool(
    title="List WiFi Broadcasts",
    description="List SSID / WiFi broadcast configurations (Integration API).",
)
async def unifi_list_wifi_broadcasts(site_id: str) -> Any:
    return await get_service().list_wifi_broadcasts(site_id)


@server.tool(
    title="List WAN Interfaces",
    description="List WAN interface configuration and status (Integration API).",
)
async def unifi_list_wans(site_id: str) -> Any:
    return await get_service().list_wans(site_id)


@server.tool(
    title="List Firewall Policies",
    description="List firewall policies (Integration API, Network 10.x+).",
)
async def unifi_list_firewall_policies(site_id: str) -> Any:
    return await get_service().list_firewall_policies(site_id)


@server.tool(
    title="List DPI Applications",
    description="List Deep Packet Inspection application catalog (Integration API).",
)
async def unifi_list_dpi_applications() -> Any:
    return await get_service().list_dpi_applications()


@server.tool(
    title="Site Health",
    description="WAN/LAN/WLAN subsystem health summary (Classic API).",
)
async def unifi_get_site_health() -> Any:
    return await get_service().get_site_health()


@server.tool(
    title="Controller Sysinfo",
    description="Controller version, uptime, and environment info (Classic API).",
)
async def unifi_get_sysinfo() -> Any:
    return await get_service().get_sysinfo()


@server.tool(
    title="List Classic Devices",
    description="Full device list with radio_table, port_table, and live stats (Classic API).",
)
async def unifi_list_classic_devices() -> Any:
    return await get_service().list_classic_devices()


@server.tool(
    title="List Classic Clients",
    description="Active wireless/wired clients with signal and throughput (Classic API).",
)
async def unifi_list_classic_clients(limit: int = 100) -> Any:
    return await get_service().list_classic_clients(limit=limit)


@server.tool(
    title="Recent Events",
    description="Recent UniFi events, newest first (Classic API).",
)
async def unifi_get_events(within_hours: int | None = 24, limit: int = 100) -> Any:
    return await get_service().get_events(within_hours=within_hours, limit=limit)


@server.tool(
    title="Recent Alarms",
    description="Active and recent alarms (Classic API).",
)
async def unifi_get_alarms(limit: int = 100) -> Any:
    return await get_service().get_alarms(limit=limit)


@server.tool(
    title="Network Anomalies",
    description=(
        "Diagnostic anomalies such as poor signal, high retries, or channel saturation "
        "(Classic API)."
    ),
)
async def unifi_get_anomalies(within_hours: int = 24) -> Any:
    return await get_service().get_anomalies(within_hours=within_hours)


@server.tool(
    title="Rogue Access Points",
    description="Nearby rogue APs detected by UniFi APs (Classic API).",
)
async def unifi_get_rogue_aps() -> Any:
    return await get_service().get_rogue_aps()


@server.tool(
    title="Gateway Statistics",
    description="Gateway WAN stats including speedtest results when available (Classic API).",
)
async def unifi_get_gateway_stats() -> Any:
    return await get_service().get_gateway_stats()


@server.tool(
    title="Site Dashboard",
    description="Aggregated WAN throughput, latency, retries, and drop rate (Classic API).",
)
async def unifi_get_dashboard() -> Any:
    return await get_service().get_dashboard()


@server.tool(
    title="Site DPI Traffic",
    description="Site-wide traffic breakdown by application/category (Classic API).",
)
async def unifi_get_site_dpi() -> Any:
    return await get_service().get_site_dpi()


@server.tool(
    title="Client DPI Traffic",
    description="Per-client DPI traffic breakdown (Classic API).",
)
async def unifi_get_client_dpi(limit: int = 50) -> Any:
    return await get_service().get_client_dpi(limit=limit)


@server.tool(
    title="IPS / IDS Events",
    description="Intrusion Prevention/Detection security events (Classic API).",
)
async def unifi_get_ips_events(limit: int = 100) -> Any:
    return await get_service().get_ips_events(limit=limit)


@server.tool(
    title="Port Anomalies",
    description="Switch port anomalies such as errors or flapping (Classic v2 API).",
)
async def unifi_get_port_anomalies() -> Any:
    return await get_service().get_port_anomalies()


@server.tool(
    title="Known Clients",
    description="Configured/known client aliases and fixed-IP mappings (Classic API).",
)
async def unifi_get_known_clients() -> Any:
    return await get_service().get_known_clients()


@server.tool(
    title="Firewall Rules",
    description="Legacy firewall rules list (Classic API).",
)
async def unifi_get_firewall_rules() -> Any:
    return await get_service().get_firewall_rules()


@server.tool(
    title="IPS Settings",
    description="IPS/IDS configuration and status (Classic API, read-only).",
)
async def unifi_get_ips_settings() -> Any:
    return await get_service().get_ips_settings()


@server.tool(
    title="Search Events",
    description="Filter recent UniFi events by keywords (e.g. 'disconnect', 'blocked', 'rogue').",
)
async def unifi_search_events(keywords: list[str], limit: int = 50) -> Any:
    return await get_service().search_events(keywords, limit=limit)


@server.tool(
    title="Troubleshoot Summary",
    description=(
        "Aggregate health, anomalies, alarms, IPS events, rogue APs, gateway, and dashboard "
        "data for AI-assisted network troubleshooting."
    ),
)
async def unifi_troubleshoot_summary() -> dict[str, Any]:
    return await get_service().build_troubleshoot_summary()


@server.custom_route("/health", methods=["GET"])
async def health_check(_request):
    from starlette.responses import JSONResponse

    return JSONResponse({"status": "ok", "service": "unify-mcp", "version": __version__})


def main() -> None:
    settings = get_settings()
    transport = settings.mcp_transport.lower()

    if transport == "stdio":
        asyncio.run(server.run_stdio_async())
    elif transport == "sse":
        asyncio.run(server.run_sse_async(host=settings.mcp_host, port=settings.mcp_port))
    elif transport in {"streamable-http", "http"}:
        asyncio.run(
            server.run_streamable_http_async(host=settings.mcp_host, port=settings.mcp_port)
        )
    else:
        raise SystemExit(f"Unsupported MCP transport: {settings.mcp_transport}")


if __name__ == "__main__":
    main()
