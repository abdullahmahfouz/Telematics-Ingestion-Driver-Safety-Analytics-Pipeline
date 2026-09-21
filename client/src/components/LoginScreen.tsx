import { useState } from "react";
import { SteeringWheel } from "@phosphor-icons/react";
import { InvalidCredentialsError, login } from "../api/authApi";
import { setToken } from "../api/authToken";
import { ErrorBanner } from "./ErrorBanner";

interface LoginScreenProps {
  onLoggedIn: () => void;
}

export function LoginScreen({ onLoggedIn }: LoginScreenProps) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (loading) return;

    setLoading(true);
    setError(null);
    try {
      const token = await login(username, password);
      setToken(token);
      onLoggedIn();
    } catch (err) {
      setError(
        err instanceof InvalidCredentialsError
          ? err.message
          : "Could not reach the API. Is it running at http://localhost:5231?",
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login-screen">
      <form className="login-card" onSubmit={handleSubmit}>
        <div className="login-card__mark" aria-hidden="true">
          <SteeringWheel size={22} weight="bold" />
        </div>
        <h1>Driver safety dashboard</h1>
        <p className="login-card__subtitle">Sign in to view live telematics data.</p>

        {error && <ErrorBanner message={error} />}

        <label>
          Username
          <input
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoFocus
            autoComplete="username"
          />
        </label>
        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
          />
        </label>

        <button type="submit" disabled={loading || !username || !password}>
          {loading ? "Signing in…" : "Sign in"}
        </button>

        <p className="login-card__hint">
          Demo login: <code>demo</code> / <code>DemoPass123!</code>
        </p>
      </form>
    </div>
  );
}
