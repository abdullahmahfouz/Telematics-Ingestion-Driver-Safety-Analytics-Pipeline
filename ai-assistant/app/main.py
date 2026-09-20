from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

from app.assistant import ask
from app.rate_limiter import assistant_rate_limiter
from app.telematics_client import telematics_client


@asynccontextmanager
async def lifespan(app: FastAPI):
    yield
    await telematics_client.aclose()


app = FastAPI(title="Telematics AI Assistant", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173"],
    allow_methods=["*"],
    allow_headers=["*"],
)


class AskRequest(BaseModel):
    question: str
    device_id: str | None = None


class AskResponse(BaseModel):
    answer: str


@app.post("/ask", response_model=AskResponse)
async def ask_endpoint(request: AskRequest) -> AskResponse:
    if not request.question.strip():
        raise HTTPException(status_code=400, detail="question must not be empty")

    allowed, retry_after = assistant_rate_limiter.try_acquire()
    if not allowed:
        raise HTTPException(
            status_code=429,
            detail=f"Rate limit exceeded. Try again in {retry_after:.0f} seconds.",
            headers={"Retry-After": str(int(retry_after) + 1)},
        )

    try:
        answer = await ask(request.question, device_id=request.device_id)
    except Exception as exc:
        raise HTTPException(status_code=502, detail=f"Assistant failed: {exc}") from exc

    return AskResponse(answer=answer)


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}
