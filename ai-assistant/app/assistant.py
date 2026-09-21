import json
import logging

from openai import AsyncOpenAI

from app.config import settings
from app.tools import TOOL_SCHEMAS, call_tool

logger = logging.getLogger("assistant")

client = AsyncOpenAI(api_key=settings.gemini_api_key, base_url=settings.gemini_base_url)

SYSTEM_PROMPT = (
    "You are a fleet safety assistant for a telematics platform. Answer questions "
    "about a vehicle's recent driving data and safety events using the tools provided. "
    "Always call a tool to get real data before answering -- never guess or make up "
    "numbers. Keep answers concise and factual."
)

FALLBACK_ANSWER = "I retrieved the data but had trouble putting together an answer. Please try asking again."


async def _get_final_answer(messages: list[dict]) -> str:
    """Calls the model for a final text answer, retrying once if it comes back
    empty. Gemini's free tier occasionally returns a blank response even on a
    200 OK -- its internal reasoning step can consume the whole token budget
    before producing visible text -- so an empty string here isn't necessarily
    our bug, but it still needs to be handled rather than shown as a blank reply."""
    for attempt in range(2):
        response = await client.chat.completions.create(
            model=settings.gemini_model,
            messages=messages,
        )
        content = response.choices[0].message.content
        if content:
            return content
        logger.warning(
            "Empty answer from model on attempt %d (finish_reason=%s)",
            attempt + 1,
            response.choices[0].finish_reason,
        )

    return FALLBACK_ANSWER


async def ask(
    question: str,
    device_id: str | None = None,
    history: list[dict] | None = None,
) -> tuple[str, list[dict]]:
    """Answers one question, optionally in the context of prior turns.

    Returns (answer, updated_history) where updated_history holds only the
    conversational turns -- the caller persists that and passes it back next time.
    """
    system_prompt = SYSTEM_PROMPT
    if device_id:
        system_prompt += (
            f" The user is currently viewing device {device_id}. If their question doesn't "
            "name a specific device, assume they mean this one."
        )

    prior_turns = history or []

    messages: list[dict] = [
        {"role": "system", "content": system_prompt},
        *prior_turns,
        {"role": "user", "content": question},
    ]

    first_response = await client.chat.completions.create(
        model=settings.gemini_model,
        messages=messages,
        tools=TOOL_SCHEMAS,
        tool_choice="auto",
    )

    response_message = first_response.choices[0].message
    tool_calls = response_message.tool_calls

    if not tool_calls:
        answer = response_message.content or FALLBACK_ANSWER
        return answer, _append_turn(prior_turns, question, answer)

    messages.append(response_message.model_dump(exclude_none=True))

    for tool_call in tool_calls:
        arguments = json.loads(tool_call.function.arguments)
        result = await call_tool(tool_call.function.name, arguments)
        messages.append(
            {
                "role": "tool",
                "tool_call_id": tool_call.id,
                "content": result,
            }
        )

    answer = await _get_final_answer(messages)
    return answer, _append_turn(prior_turns, question, answer)


def _append_turn(prior_turns: list[dict], question: str, answer: str) -> list[dict]:
    return [
        *prior_turns,
        {"role": "user", "content": question},
        {"role": "assistant", "content": answer},
    ]
