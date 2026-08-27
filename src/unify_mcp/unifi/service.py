from __future__ import annotations

from typing import Any

from unify_mcp.unifi.client import UniFiClient


class UniFiService:
    """High-level read-only UniFi operations grouped for MCP tools."""

    def __init__(self, client: UniFiClient | None = None) -> None:
        self.client = client or UniFiClient()

    async def close(self) -> None:
        await self.client.close()

    # --- Integration API (X-API-KEY) ---

    async def get_app_info(self) -> Any:
        return await self.client.integration_get("/info")

    async def list_sites(self) -> Any:
        return await self.client.integration_get("/sites")

    async def list_devices(self, site_id: str) -> Any:
        return await self.client.integration_get(f"/sites/{site_id}/devices")

    async def get_device(self, site_id: str, device_id: str) -> Any:
        return await self.client.integration_get(f"/sites/{site_id}/devices/{device_id}")

    async def get_device_stats(self, site_id: str, device_id: str) -> Any:
        return await self.client.integration_get(
            f"/sites/{site_id}/devices/{device_id}/statistics/latest"
        )

    async def list_clients(self, site_id: str, *, limit: int = 200, offset: int = 0) -> Any:
        return await self.client.integration_get(
            f"/sites/{site_id}/clients",
            params={"limit": limit, "offset": offset},
        )

    async def get_client(self, site_id: str, client_id: str) -> Any:
        return await self.client.integration_get(f"/sites/{site_id}/clients/{client_id}")

    async def list_networks(self, site_id: str) -> Any:
        return await self.client.integration_get(f"/sites/{site_id}/networks")

    async def list_wifi_broadcasts(self, site_id: str) -> Any:
        return await self.client.integration_get(f"/sites/{site_id}/wifi/broadcasts")

    async def list_wans(self, site_id: str) -> Any:
        return await self.client.integration_get(f"/sites/{site_id}/wans")

    async def list_firewall_policies(self, site_id: str) -> Any:
        return await self.client.integration_get(f"/sites/{site_id}/firewall/policies")

    async def list_dpi_applications(self) -> Any:
        return await self.client.integration_get("/dpi/applications")

    # --- Classic API (cookie auth) ---

    async def get_site_health(self) -> Any:
        payload = await self.client.classic_get("health")
        return UniFiClient.summarize_health(payload)

    async def get_sysinfo(self) -> Any:
        return await self.client.classic_get("sysinfo")

    async def list_classic_devices(self) -> Any:
        return await self.client.classic_get("devices")

    async def list_classic_clients(self, *, limit: int = 100) -> Any:
        return await self.client.classic_get("clients", params={"_limit": limit})

    async def get_events(self, *, within_hours: int | None = None, limit: int = 100) -> Any:
        params: dict[str, Any] = {"_limit": limit, "_sort": "-time"}
        if within_hours is not None:
            params["within"] = within_hours
        return await self.client.classic_get("events", params=params)

    async def get_alarms(self, *, limit: int = 100) -> Any:
        return await self.client.classic_get("alarms", params={"_limit": limit, "_sort": "-time"})

    async def get_anomalies(self, *, within_hours: int | None = 24) -> Any:
        params = {"within": within_hours} if within_hours is not None else None
        return await self.client.classic_get("anomalies", params=params)

    async def get_rogue_aps(self) -> Any:
        return await self.client.classic_get("rogue_aps")

    async def get_gateway_stats(self) -> Any:
        return await self.client.classic_get("gateway")

    async def get_dashboard(self) -> Any:
        return await self.client.classic_get("dashboard")

    async def get_site_dpi(self) -> Any:
        return await self.client.classic_get("site_dpi")

    async def get_client_dpi(self, *, limit: int = 50) -> Any:
        return await self.client.classic_get("client_dpi", params={"_limit": limit})

    async def get_ips_events(self, *, limit: int = 100) -> Any:
        return await self.client.classic_get("ips_events", params={"_limit": limit})

    async def get_port_anomalies(self) -> Any:
        return await self.client.classic_v2_get("ports/port-anomalies")

    async def get_known_clients(self) -> Any:
        return await self.client.classic_get("known_clients")

    async def get_firewall_rules(self) -> Any:
        return await self.client.classic_get("firewall_rules")

    async def get_ips_settings(self) -> Any:
        return await self.client.classic_get("settings_ips")

    async def search_events(self, keywords: list[str], *, limit: int = 50) -> Any:
        payload = await self.get_events(limit=max(limit, 200))
        events = payload.get("data", payload if isinstance(payload, list) else [])
        if not isinstance(events, list):
            return {"matches": [], "note": "Unexpected events payload shape"}
        matches = UniFiClient.filter_events(events, keywords=keywords, limit=limit)
        return {"keywords": keywords, "count": len(matches), "matches": matches}

    async def build_troubleshoot_summary(self) -> dict[str, Any]:
        """Aggregate health, anomalies, alarms, and recent security signals."""
        summary: dict[str, Any] = {"issues": [], "signals": {}}

        try:
            summary["health"] = await self.get_site_health()
        except Exception as exc:  # noqa: BLE001
            summary["issues"].append(f"health: {exc}")

        for name, coro in [
            ("anomalies", self.get_anomalies(within_hours=24)),
            ("alarms", self.get_alarms(limit=25)),
            ("ips_events", self.get_ips_events(limit=25)),
            ("rogue_aps", self.get_rogue_aps()),
            ("gateway", self.get_gateway_stats()),
            ("dashboard", self.get_dashboard()),
        ]:
            try:
                payload = await coro
                data = payload.get("data", payload) if isinstance(payload, dict) else payload
                summary["signals"][name] = data
                if isinstance(data, list) and data:
                    summary["issues"].append(f"{name}: {len(data)} recent item(s)")
            except Exception as exc:  # noqa: BLE001
                summary["signals"][name] = {"error": str(exc)}

        return summary
