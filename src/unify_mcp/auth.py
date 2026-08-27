from __future__ import annotations

from mcp.server.auth.provider import AccessToken


class StaticTokenVerifier:
    """Validate MCP bearer tokens against a single configured secret."""

    def __init__(self, expected_token: str) -> None:
        self._expected_token = expected_token

    async def verify_token(self, token: str) -> AccessToken | None:
        if token != self._expected_token:
            return None
        return AccessToken(token=token, client_id="unify-mcp", scopes=[])
