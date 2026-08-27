from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from unify_mcp.unifi.client import ReadOnlyViolationError, UniFiClient


def test_blocks_non_get_methods():
    client = UniFiClient(settings=MagicMock(), secrets=MagicMock())
    with pytest.raises(ReadOnlyViolationError):
        client._assert_get_only("POST")


def test_blocks_action_paths():
    client = UniFiClient(settings=MagicMock(), secrets=MagicMock())
    with pytest.raises(ReadOnlyViolationError):
        client._assert_read_only_path("/v1/sites/abc/devices/123/actions")


def test_summarize_health():
    payload = {
        "data": [
            {
                "subsystem": "wan",
                "status": "ok",
                "latency": 12,
                "tx_bytes": 100,
                "rx_bytes": 200,
            }
        ]
    }
    summary = UniFiClient.summarize_health(payload)
    assert summary["subsystems"]["wan"]["status"] == "ok"


def test_filter_events_keywords():
    events = [
        {"msg": "User disconnected", "key": "evt1"},
        {"msg": "AP adopted", "key": "evt2"},
        {"msg": "Client blocked by firewall", "key": "evt3"},
    ]
    matches = UniFiClient.filter_events(events, keywords=["disconnect", "blocked"], limit=10)
    assert len(matches) == 2


@pytest.mark.asyncio
async def test_integration_get_builds_url():
    settings = MagicMock()
    settings.unifi_verify_ssl = False
    settings.unifi_request_timeout_seconds = 10

    secrets = MagicMock()
    secrets.get_controller_url.return_value = "https://unifi.local"
    secrets.get_unifi_api_key.return_value = "test-key"

    client = UniFiClient(settings=settings, secrets=secrets)

    mock_response = MagicMock()
    mock_response.json.return_value = {"version": "10.0.0"}
    mock_response.raise_for_status = MagicMock()

    with patch("unify_mcp.unifi.client.httpx.AsyncClient") as mock_client_cls:
        mock_client = AsyncMock()
        mock_client.__aenter__.return_value = mock_client
        mock_client.__aexit__.return_value = None
        mock_client.get.return_value = mock_response
        mock_client_cls.return_value = mock_client

        result = await client.integration_get("/info")

    assert result == {"version": "10.0.0"}
    called_url = mock_client.get.call_args.args[0]
    assert called_url == "https://unifi.local/proxy/network/integration/v1/info"
