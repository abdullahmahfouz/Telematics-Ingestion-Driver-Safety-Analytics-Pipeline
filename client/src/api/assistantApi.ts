import { authHeaders, requireSession } from "./authToken";

const ASSISTANT_BASE_URL = "http://localhost:8000";

export class AssistantRateLimitError extends Error {
  constructor(message?: string) {
    super(
      message ??
        "The assistant is rate-limited (free tier allows a few requests per minute). Wait a moment and try again.",
    );
    this.name = "AssistantRateLimitError";
  }
}

export async function askAssistant(
  question: string,
  deviceId?: string,
  conversationId?: string,
): Promise<string> {
  const response = await fetch(`${ASSISTANT_BASE_URL}/ask`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify({ question, device_id: deviceId, conversation_id: conversationId }),
  });

  requireSession(response);

  // 429 = our own service's rate limiter rejected the request before ever calling Gemini.
  if (response.status === 429) {
    const problem = await response.json().catch(() => null);
    throw new AssistantRateLimitError(
      typeof problem?.detail === "string" ? problem.detail : undefined,
    );
  }

  // 502 = the request got through our limiter, but Gemini itself rejected it (its own rate limit).
  if (response.status === 502) {
    const problem = await response.json().catch(() => null);
    if (typeof problem?.detail === "string" && problem.detail.includes("429")) {
      throw new AssistantRateLimitError();
    }
    throw new Error(problem?.detail ?? "The assistant failed to answer.");
  }

  if (!response.ok) {
    throw new Error(`Assistant request failed with status ${response.status}`);
  }

  const data = (await response.json()) as { answer: string };
  return data.answer;
}

/** Drops the server-side memory for a conversation. Best-effort: if it fails,
 * the client still starts a fresh conversation id, so the old one just ages out. */
export async function clearConversation(conversationId: string): Promise<void> {
  await fetch(`${ASSISTANT_BASE_URL}/conversations/${conversationId}`, {
    method: "DELETE",
    headers: authHeaders(),
  }).catch(() => undefined);
}
