from __future__ import annotations

import re
from typing import Any

import httpx

from unify_mcp.config import Settings, get_settings
from unify_mcp.secrets import SecretStore, get_secret_store

INTEGRATION_PREFIX = "/proxy/network/integration/v1"
CLASSIC_PREFIX = "/proxy/network/api/s"

# Paths that mutate state even when invoked with GET in some builds.
BLOCKED_PATH_FRAGMENTS = (
    "/actions",
    "/cmd/",
    "/rest/",
    "speedtest-status",  # status poll tied to active speedtest trigger
)

# Classic GET endpoints used for diagnostics and troubleshooting.
CLASSIC_READ_PATHS = {
    "health": "stat/health",
    "sysinfo": "stat/sysinfo",
    "devices": "stat/device",
    "devices_basic": "stat/device-basic",
    "clients": "stat/sta",
    "clients_all": "stat/alluser",
    "events": "stat/event",
    "alarms": "stat/alarm",
    "anomalies": "stat/anomalies",
    "rogue_aps": "stat/rogueap",
    "gateway": "stat/gateway",
    "dashboard": "stat/dashboard",
    "site_dpi": "stat/sitedpi",
    "client_dpi": "stat/stadpi",
    "ips_events": "stat/ips/event",
    "sessions": "stat/session",
    "port_anomalies": "ports/port-anomalies",
    "known_clients": "rest/user",
    "networks": "rest/networkconf",
    "wlans": "rest/wlanconf",
    "firewall_rules": "rest/firewallrule",
    "port_forwards": "rest/portforward",
    "settings_ips": "rest/setting/ips",
}


class ReadOnlyViolationError(RuntimeError):
    """Raised when a request would mutate UniFi state."""


class UniFiClient:
    """Read-only HTTP client for UniFi Integration + Classic APIs."""

    def __init__(
        self,
        settings: Settings | None = None,
        secrets: SecretStore | None = None,
    ) -> None:
        self.settings = settings or get_settings()
        self.secrets = secrets or get_secret_store()
        self.base_url = self.secrets.get_controller_url()
        self.api_key = self.secrets.get_unifi_api_key()
        self.site = self.settings.unifi_site
        self._classic_session: httpx.AsyncClient | None = None
        self._classic_logged_in = False

    async def close(self) -> None:
        if self._classic_session:
            await self._classic_session.aclose()
            self._classic_session = None
            self._classic_logged_in = False

    def _integration_url(self, path: str) -> str:
        normalized = path if path.startswith("/") else f"/{path}"
        if normalized.startswith("/v1/"):
            normalized = normalized[len("/v1") :]
        elif normalized == "/v1":
            normalized = "/"
        return f"{self.base_url}{INTEGRATION_PREFIX}{normalized}"

    def _classic_url(self, path: str) -> str:
        normalized = path.lstrip("/")
        if normalized.startswith(("stat/", "rest/", "v2/")):
            suffix = normalized
        else:
            suffix = CLASSIC_READ_PATHS.get(normalized, normalized)
        return f"{self.base_url}{CLASSIC_PREFIX}/{self.site}/{suffix}"

    def _assert_read_only_path(self, path: str) -> None:
        lowered = path.lower()
        for fragment in BLOCKED_PATH_FRAGMENTS:
            if fragment in lowered:
                raise ReadOnlyViolationError(
                    f"Blocked read-only violation: path contains '{fragment}'"
                )

    def _assert_get_only(self, method: str) -> None:
        if method.upper() != "GET":
            raise ReadOnlyViolationError(
                f"Only GET requests are allowed; received {method.upper()}"
            )

    async def integration_get(
        self,
        path: str,
        *,
        params: dict[str, Any] | None = None,
    ) -> Any:
        self._assert_get_only("GET")
        self._assert_read_only_path(path)
        url = self._integration_url(path)
        async with httpx.AsyncClient(
            verify=self.settings.unifi_verify_ssl,
            timeout=self.settings.unifi_request_timeout_seconds,
        ) as client:
            response = await client.get(
                url,
                headers={"X-API-KEY": self.api_key, "Accept": "application/json"},
                params=params,
            )
            response.raise_for_status()
            return response.json()

    async def _ensure_classic_session(self) -> httpx.AsyncClient:
        if self._classic_session and self._classic_logged_in:
            return self._classic_session

        credentials = self.secrets.get_classic_credentials()
        if not credentials:
            raise RuntimeError(
                "Classic UniFi diagnostics require UNIFI_USERNAME and UNIFI_PASSWORD "
                "secrets in Azure Key Vault (or env overrides). Create a View Only local "
                "admin and store credentials as 'unifi-username' / 'unifi-password'."
            )

        username, password = credentials
        client = httpx.AsyncClient(
            base_url=self.base_url,
            verify=self.settings.unifi_verify_ssl,
            timeout=self.settings.unifi_request_timeout_seconds,
            headers={"Accept": "application/json"},
        )

        login = await client.post(
            "/api/auth/login",
            json={"username": username, "password": password, "remember": True},
        )
        login.raise_for_status()

        csrf = login.headers.get("x-csrf-token") or login.headers.get("x-updated-csrf-token")
        if csrf:
            client.headers["X-CSRF-Token"] = csrf

        self._classic_session = client
        self._classic_logged_in = True
        return client

    async def classic_get(
        self,
        path: str,
        *,
        params: dict[str, Any] | None = None,
    ) -> Any:
        self._assert_get_only("GET")
        self._assert_read_only_path(path)
        client = await self._ensure_classic_session()
        url = self._classic_url(path)

        response = await client.get(url, params=params)
        if response.status_code == 401:
            self._classic_logged_in = False
            client = await self._ensure_classic_session()
            response = await client.get(url, params=params)

        response.raise_for_status()
        payload = response.json()
        if isinstance(payload, dict) and payload.get("meta", {}).get("rc") == "error":
            raise RuntimeError(payload.get("meta", {}).get("msg", "UniFi classic API error"))
        return payload

    async def classic_v2_get(
        self,
        path: str,
        *,
        params: dict[str, Any] | None = None,
    ) -> Any:
        self._assert_get_only("GET")
        self._assert_read_only_path(path)
        client = await self._ensure_classic_session()
        normalized = path.lstrip("/")
        url = f"/proxy/network/v2/api/site/{self.site}/{normalized}"
        response = await client.get(url, params=params)
        response.raise_for_status()
        return response.json()

    async def ping(self) -> dict[str, Any]:
        try:
            info = await self.integration_get("/info")
            return {"reachable": True, "api": "integration", "info": info}
        except Exception as integration_error:  # noqa: BLE001
            try:
                health = await self.classic_get("health")
                return {
                    "reachable": True,
                    "api": "classic",
                    "health": health,
                    "integration_error": str(integration_error),
                }
            except Exception as classic_error:  # noqa: BLE001
                return {
                    "reachable": False,
                    "integration_error": str(integration_error),
                    "classic_error": str(classic_error),
                }

    @staticmethod
    def summarize_health(health_payload: dict[str, Any]) -> dict[str, Any]:
        data = health_payload.get("data", health_payload)
        if isinstance(data, list):
            subsystems = {
                item.get("subsystem"): {
                    "status": item.get("status"),
                    "num_user": item.get("num_user"),
                    "num_guest": item.get("num_guest"),
                    "num_ap": item.get("num_ap"),
                    "num_adopted": item.get("num_adopted"),
                    "num_disconnected": item.get("num_disconnected"),
                    "num_pending": item.get("num_pending"),
                    "wan_ip": item.get("wan_ip"),
                    "tx_bytes": item.get("tx_bytes"),
                    "rx_bytes": item.get("rx_bytes"),
                    "latency": item.get("latency"),
                }
                for item in data
                if isinstance(item, dict)
            }
            return {"subsystems": subsystems}
        return {"raw": data}

    @staticmethod
    def filter_events(
        events: list[dict[str, Any]],
        *,
        keywords: list[str] | None = None,
        limit: int = 50,
    ) -> list[dict[str, Any]]:
        if not keywords:
            return events[:limit]

        pattern = re.compile("|".join(re.escape(word) for word in keywords), re.IGNORECASE)
        filtered = [
            event
            for event in events
            if pattern.search(str(event.get("msg", "")))
            or pattern.search(str(event.get("key", "")))
        ]
        return filtered[:limit]
