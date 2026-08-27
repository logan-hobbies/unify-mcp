from __future__ import annotations

from functools import lru_cache

from azure.core.exceptions import ResourceNotFoundError
from azure.identity import ClientSecretCredential, DefaultAzureCredential
from azure.keyvault.secrets import SecretClient

from unify_mcp.config import Settings, get_settings


class SecretStore:
    """Loads UniFi credentials from Azure Key Vault."""

    def __init__(self, settings: Settings | None = None) -> None:
        self._settings = settings or get_settings()
        self._client = SecretClient(
            vault_url=self._settings.azure_key_vault_url,
            credential=self._build_credential(),
        )
        self._cache: dict[str, str] = {}

    def _build_credential(self):
        settings = self._settings
        if settings.azure_tenant_id and settings.azure_client_id and settings.azure_client_secret:
            return ClientSecretCredential(
                tenant_id=settings.azure_tenant_id,
                client_id=settings.azure_client_id,
                client_secret=settings.azure_client_secret.get_secret_value(),
            )
        return DefaultAzureCredential()

    def get_secret(self, name: str, *, required: bool = True) -> str | None:
        if name in self._cache:
            return self._cache[name]

        try:
            value = self._client.get_secret(name).value
        except ResourceNotFoundError:
            if required:
                raise
            return None

        if value is None:
            if required:
                raise ValueError(f"Secret '{name}' exists but has no value")
            return None

        self._cache[name] = value
        return value

    def get_unifi_api_key(self) -> str:
        value = self.get_secret(self._settings.unifi_api_key_secret_name, required=True)
        assert value is not None
        return value

    def get_controller_url(self) -> str:
        if self._settings.unifi_controller_url:
            return self._settings.unifi_controller_url.rstrip("/")

        secret_name = self._settings.unifi_controller_url_secret_name
        if secret_name:
            value = self.get_secret(secret_name, required=False)
            if value:
                return value.rstrip("/")

        raise ValueError(
            "UniFi controller URL not configured. Set UNIFI_CONTROLLER_URL or store "
            f"'{secret_name}' in Azure Key Vault."
        )

    def get_classic_credentials(self) -> tuple[str, str] | None:
        username_name = self._settings.unifi_username_secret_name
        password_name = self._settings.unifi_password_secret_name
        if not username_name or not password_name:
            return None

        username = self.get_secret(username_name, required=False)
        password = self.get_secret(password_name, required=False)
        if username and password:
            return username, password
        return None


@lru_cache
def get_secret_store() -> SecretStore:
    return SecretStore()
