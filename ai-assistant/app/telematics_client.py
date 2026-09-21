import httpx

from app.config import settings


class TelematicsClient:
    """Thin wrapper over the .NET API's read endpoints. This is the only place
    the assistant talks to the rest of the platform — it never touches the
    database directly, it just calls the same REST endpoints TelematicsQueryService
    already exposes."""

    def __init__(self) -> None:
        self._client = httpx.AsyncClient(base_url=settings.telematics_api_base_url, timeout=10.0)

    async def get_recent_records(self, device_id: str, limit: int = 10) -> list[dict]:
        response = await self._client.get(f"/{device_id}/recent", params={"limit": limit})
        response.raise_for_status()
        return response.json()

    async def get_harsh_braking_count(self, device_id: str, since_hours: int = 24) -> dict:
        response = await self._client.get(f"/{device_id}/harsh-braking-count", params={"sinceHours": since_hours})
        response.raise_for_status()
        return response.json()

    async def get_harsh_cornering_count(self, device_id: str, since_hours: int = 24) -> dict:
        response = await self._client.get(f"/{device_id}/harsh-cornering-count", params={"sinceHours": since_hours})
        response.raise_for_status()
        return response.json()

    async def get_harsh_acceleration_count(self, device_id: str, since_hours: int = 24) -> dict:
        response = await self._client.get(
            f"/{device_id}/harsh-acceleration-count", params={"sinceHours": since_hours}
        )
        response.raise_for_status()
        return response.json()

    async def aclose(self) -> None:
        await self._client.aclose()


telematics_client = TelematicsClient()
