import { afterEach, describe, expect, it, vi } from "vitest";
import { askAssistant, AssistantRateLimitError } from "./assistantApi";

function mockFetch(response: { status: number; body: unknown }) {
  return vi.fn().mockResolvedValue({
    status: response.status,
    ok: response.status >= 200 && response.status < 300,
    json: async () => response.body,
  });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("askAssistant", () => {
  it("returns the answer on success", async () => {
    vi.stubGlobal("fetch", mockFetch({ status: 200, body: { answer: "64 harsh braking events." } }));

    await expect(askAssistant("how many?", "b2A83F1")).resolves.toBe("64 harsh braking events.");
  });

  it("sends the question and device id in the request body", async () => {
    const fetchMock = mockFetch({ status: 200, body: { answer: "ok" } });
    vi.stubGlobal("fetch", fetchMock);

    await askAssistant("is it safe?", "b2A83F1");

    const [, init] = fetchMock.mock.calls[0];
    expect(JSON.parse(init.body)).toEqual({ question: "is it safe?", device_id: "b2A83F1" });
  });

  it("throws a rate limit error when our own service rejects with 429", async () => {
    vi.stubGlobal(
      "fetch",
      mockFetch({ status: 429, body: { detail: "Rate limit exceeded. Try again in 42 seconds." } }),
    );

    await expect(askAssistant("how many?")).rejects.toBeInstanceOf(AssistantRateLimitError);
  });

  it("surfaces the retry time from our own rate limiter", async () => {
    vi.stubGlobal(
      "fetch",
      mockFetch({ status: 429, body: { detail: "Rate limit exceeded. Try again in 42 seconds." } }),
    );

    await expect(askAssistant("how many?")).rejects.toThrow(/42 seconds/);
  });

  it("throws a rate limit error when Gemini itself rejects, wrapped as a 502", async () => {
    vi.stubGlobal(
      "fetch",
      mockFetch({ status: 502, body: { detail: "Assistant failed: Error code: 429 - quota exceeded" } }),
    );

    await expect(askAssistant("how many?")).rejects.toBeInstanceOf(AssistantRateLimitError);
  });

  it("throws a generic error for a non-rate-limit 502", async () => {
    vi.stubGlobal("fetch", mockFetch({ status: 502, body: { detail: "Assistant failed: connection refused" } }));

    const promise = askAssistant("how many?");
    await expect(promise).rejects.toThrow(/connection refused/);
    await expect(promise).rejects.not.toBeInstanceOf(AssistantRateLimitError);
  });
});
