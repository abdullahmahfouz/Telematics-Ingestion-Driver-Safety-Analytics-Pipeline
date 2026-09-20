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

export async function askAssistant(question: string, deviceId?: string): Promise<string> {
  const response = await fetch(`${ASSISTANT_BASE_URL}/ask`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ question, device_id: deviceId }),
  });

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
