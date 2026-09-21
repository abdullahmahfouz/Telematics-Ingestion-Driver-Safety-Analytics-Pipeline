import { useRef, useState } from "react";
import { SteeringWheel } from "@phosphor-icons/react";
import { InvalidCredentialsError, LoginRateLimitError, login } from "../api/authApi";
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
  // Synchronous guard, checked before any state update: `if (loading) return` alone has a
  // race window between a fast double-submit and React committing the re-render.
  const loadingRef = useRef(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (loadingRef.current) return;

    loadingRef.current = true;
    setLoading(true);
    setError(null);
    try {
      const token = await login(username, password);
      setToken(token);
      onLoggedIn();
    } catch (err) {
      if (err instanceof InvalidCredentialsError || err instanceof LoginRateLimitError) {
        setError(err.message);
      } else {
        setError("Could not reach the API. Is it running at http://localhost:5231?");
      }
    } finally {
      loadingRef.current = false;
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
