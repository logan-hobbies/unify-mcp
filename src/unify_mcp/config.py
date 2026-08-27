from functools import lru_cache

from pydantic import Field, SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Runtime configuration for the UniFi MCP server."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    # Azure Key Vault
    azure_key_vault_url: str = Field(
        ...,
        description="Azure Key Vault URL, e.g. https://my-vault.vault.azure.net/",
    )
    unifi_api_key_secret_name: str = Field(
        default="unifi-api-key",
        description="Key Vault secret name for the UniFi X-API-KEY",
    )
    unifi_controller_url_secret_name: str | None = Field(
        default="unifi-controller-url",
        description="Optional Key Vault secret for the controller base URL",
    )
    unifi_username_secret_name: str | None = Field(
        default="unifi-username",
        description="Optional Key Vault secret for classic API username",
    )
    unifi_password_secret_name: str | None = Field(
        default="unifi-password",
        description="Optional Key Vault secret for classic API password",
    )

    # Non-secret overrides (env wins over Key Vault for URL when set)
    unifi_controller_url: str | None = Field(
        default=None,
        description="UniFi console base URL, e.g. https://192.168.1.1",
    )
    unifi_site: str = Field(
        default="default",
        description="Classic API site slug (usually 'default')",
    )
    unifi_verify_ssl: bool = Field(
        default=False,
        description="Verify TLS certificates for the local UniFi controller",
    )
    unifi_request_timeout_seconds: float = Field(default=30.0, ge=5.0, le=120.0)

    # MCP server
    mcp_host: str = Field(default="0.0.0.0")
    mcp_port: int = Field(default=8080, ge=1, le=65535)
    mcp_transport: str = Field(
        default="streamable-http",
        description="MCP transport: streamable-http | sse | stdio",
    )
    mcp_auth_token: SecretStr | None = Field(
        default=None,
        description="Optional bearer token required on MCP HTTP transports",
    )

    # Azure credential (optional explicit service principal)
    azure_tenant_id: str | None = None
    azure_client_id: str | None = None
    azure_client_secret: SecretStr | None = None


@lru_cache
def get_settings() -> Settings:
    return Settings()
