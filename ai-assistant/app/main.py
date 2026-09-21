from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from openai import APIStatusError
from pydantic import BaseModel

from app.assistant import ask
from app.auth import get_bearer_token
from app.conversation_store import conversation_store
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
    conversation_id: str | None = None


class AskResponse(BaseModel):
    answer: str


@app.post("/ask", response_model=AskResponse)
async def ask_endpoint(request: AskRequest, auth_token: str = Depends(get_bearer_token)) -> AskResponse:
    if not request.question.strip():
        raise HTTPException(status_code=400, detail="question must not be empty")

    allowed, retry_after = assistant_rate_limiter.try_acquire()
    if not allowed:
        raise HTTPException(
            status_code=429,
            detail=f"Rate limit exceeded. Try again in {retry_after:.0f} seconds.",
            headers={"Retry-After": str(int(retry_after) + 1)},
        )

    history = conversation_store.get_messages(request.conversation_id) if request.conversation_id else []

    try:
        answer, updated_history = await ask(
            request.question,
            device_id=request.device_id,
            history=history,
            auth_token=auth_token,
        )
    except APIStatusError as exc:
        # 503 is Gemini's own "temporarily overloaded" response -- common on the free
        # tier -- and worth a plain-language message instead of the raw SDK repr.
        # Other status codes (e.g. 429) keep the raw detail: the frontend pattern-matches
        # the string "429" in it to tell Gemini's own rate limit apart from other failures.
        if exc.status_code == 503:
            raise HTTPException(
                status_code=502,
                detail="The assistant model is temporarily overloaded (Gemini is at capacity). Please try again in a moment.",
            ) from exc
        raise HTTPException(status_code=502, detail=f"Assistant failed: {exc}") from exc
    except Exception as exc:
        raise HTTPException(status_code=502, detail=f"Assistant failed: {exc}") from exc

    if request.conversation_id:
        conversation_store.save_messages(request.conversation_id, updated_history)

    return AskResponse(answer=answer)


@app.delete("/conversations/{conversation_id}", status_code=204)
async def clear_conversation(conversation_id: str, _auth_token: str = Depends(get_bearer_token)) -> None:
    conversation_store.clear(conversation_id)


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}
