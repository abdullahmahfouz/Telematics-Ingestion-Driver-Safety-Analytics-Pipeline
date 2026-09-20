import { useState } from "react";
import ReactMarkdown from "react-markdown";
import { askAssistant, AssistantRateLimitError } from "../api/assistantApi";

interface QaEntry {
  question: string;
  answer: string;
}

interface AssistantPanelProps {
  deviceId: string;
}

export function AssistantPanel({ deviceId }: AssistantPanelProps) {
  const [question, setQuestion] = useState("");
  const [history, setHistory] = useState<QaEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = question.trim();
    if (!trimmed || loading) return;

    setLoading(true);
    setError(null);
    try {
      const answer = await askAssistant(trimmed, deviceId);
      setHistory((prev) => [...prev, { question: trimmed, answer }]);
      setQuestion("");
    } catch (err) {
      if (err instanceof AssistantRateLimitError) {
        setError(err.message);
      } else {
        setError("Could not reach the assistant. Is it running at http://localhost:8000?");
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="assistant-panel">
      <h2>Ask the safety assistant</h2>

      {history.length === 0 && !loading && (
        <p className="empty-state">
          Try: "How many harsh braking events has b2A83F1 had today?" or "Is this device driving safely?"
        </p>
      )}

      <div className="assistant-history">
        {history.map((entry, i) => (
          <div className="assistant-entry" key={i}>
            <p className="assistant-entry__question">{entry.question}</p>
            <div className="assistant-entry__answer">
              <ReactMarkdown>{entry.answer}</ReactMarkdown>
            </div>
          </div>
        ))}
        {loading && <p className="assistant-entry__answer assistant-entry__answer--pending">Thinking…</p>}
      </div>

      {error && <div className="banner banner--error">{error}</div>}

      <form className="assistant-form" onSubmit={handleSubmit}>
        <input
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="Ask about this device's safety data…"
          aria-label="Question for the assistant"
          disabled={loading}
        />
        <button type="submit" disabled={loading || !question.trim()}>
          {loading ? "Asking…" : "Ask"}
        </button>
      </form>
    </section>
  );
}
