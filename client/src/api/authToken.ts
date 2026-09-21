const STORAGE_KEY = "telematics.authToken";

/** sessionStorage, not localStorage -- a demo login shouldn't outlive the tab. */
export function getToken(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

export function setToken(token: string): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, token);
  } catch {
    // Storage unavailable (e.g. private browsing) -- the session just won't persist.
  }
}

export function clearToken(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // ignore
  }
}

export function authHeaders(): HeadersInit {
  const token = getToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export class SessionExpiredError extends Error {
  constructor() {
    super("Your session has expired. Please log in again.");
    this.name = "SessionExpiredError";
  }
}

/** Clears the stored token and throws if the response says the token was rejected. */
export function requireSession(response: Response): void {
  if (response.status === 401) {
    clearToken();
    throw new SessionExpiredError();
  }
}
