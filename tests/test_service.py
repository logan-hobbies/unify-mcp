from unittest.mock import AsyncMock, MagicMock

import pytest

from unify_mcp.unifi.service import UniFiService


@pytest.mark.asyncio
async def test_troubleshoot_summary_collects_signals():
    client = MagicMock()
    service = UniFiService(client=client)

    service.get_site_health = AsyncMock(return_value={"subsystems": {"wan": {"status": "ok"}}})
    service.get_anomalies = AsyncMock(return_value={"data": [{"anomaly": "poor_signal"}]})
    service.get_alarms = AsyncMock(return_value={"data": []})
    service.get_ips_events = AsyncMock(return_value={"data": []})
    service.get_rogue_aps = AsyncMock(return_value={"data": []})
    service.get_gateway_stats = AsyncMock(return_value={"data": [{}]})
    service.get_dashboard = AsyncMock(return_value={"data": [{}]})

    summary = await service.build_troubleshoot_summary()

    assert "health" in summary
    assert "anomalies" in summary["signals"]
    assert any("anomalies" in issue for issue in summary["issues"])
