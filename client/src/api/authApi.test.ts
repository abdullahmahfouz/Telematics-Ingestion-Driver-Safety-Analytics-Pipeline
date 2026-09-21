import { afterEach, describe, expect, it, vi } from "vitest";
import { InvalidCredentialsError, login } from "./authApi";

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

describe("login", () => {
  it("returns the token on success", async () => {
    vi.stubGlobal("fetch", mockFetch({ status: 200, body: { token: "jwt-token", expiresAt: "2026-01-01T00:00:00Z" } }));

    await expect(login("demo", "DemoPass123!")).resolves.toBe("jwt-token");
  });

  it("sends the username and password in the request body", async () => {
    const fetchMock = mockFetch({ status: 200, body: { token: "jwt-token" } });
    vi.stubGlobal("fetch", fetchMock);

    await login("demo", "DemoPass123!");

    const [, init] = fetchMock.mock.calls[0];
    expect(JSON.parse(init.body)).toEqual({ username: "demo", password: "DemoPass123!" });
  });

  it("throws InvalidCredentialsError on 401", async () => {
    vi.stubGlobal("fetch", mockFetch({ status: 401, body: { detail: "Invalid username or password." } }));

    await expect(login("demo", "wrong")).rejects.toBeInstanceOf(InvalidCredentialsError);
  });

  it("throws a generic error for other failures", async () => {
    vi.stubGlobal("fetch", mockFetch({ status: 500, body: {} }));

    await expect(login("demo", "DemoPass123!")).rejects.toThrow(/status 500/);
  });
});
