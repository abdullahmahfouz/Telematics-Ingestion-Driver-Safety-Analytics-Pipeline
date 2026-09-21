const AUTH_BASE_URL = "http://localhost:5231/api/auth";

export class InvalidCredentialsError extends Error {
  constructor() {
    super("Invalid username or password.");
    this.name = "InvalidCredentialsError";
  }
}

export async function login(username: string, password: string): Promise<string> {
  const response = await fetch(`${AUTH_BASE_URL}/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });

  if (response.status === 401) {
    throw new InvalidCredentialsError();
  }
  if (!response.ok) {
    throw new Error(`Login failed with status ${response.status}`);
  }

  const data = (await response.json()) as { token: string };
  return data.token;
}
