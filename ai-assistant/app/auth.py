import jwt
from fastapi import Header, HTTPException

from app.config import settings


def get_bearer_token(authorization: str | None = Header(default=None)) -> str:
    """Validates the bearer JWT issued by the .NET API's POST /api/auth/login and returns
    the raw token so it can be forwarded to the API's own protected endpoints -- the
    assistant queries telematics data as the caller, not as some separate service identity."""
    if not authorization or not authorization.lower().startswith("bearer "):
        raise HTTPException(status_code=401, detail="Missing bearer token.")

    token = authorization.split(" ", 1)[1]

    try:
        jwt.decode(
            token,
            settings.jwt_signing_key,
            algorithms=["HS256"],
            issuer=settings.jwt_issuer,
            audience=settings.jwt_audience,
        )
    except jwt.PyJWTError as exc:
        raise HTTPException(status_code=401, detail="Invalid or expired token.") from exc

    return token
