from __future__ import annotations

from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from unify_mcp.unifi.service import UniFiService

_service: UniFiService | None = None


def get_service() -> UniFiService:
    global _service
    if _service is None:
        _service = UniFiService()
    return _service


@asynccontextmanager
async def lifespan_service() -> AsyncIterator[UniFiService]:
    service = get_service()
    try:
        yield service
    finally:
        await service.close()
