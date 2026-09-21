import { useState } from "react";
import ReactMarkdown from "react-markdown";
import { ArrowCounterClockwise, ChatCircleDots, PaperPlaneTilt } from "@phosphor-icons/react";
import { askAssistant, AssistantRateLimitError, clearConversation } from "../api/assistantApi";
import { ErrorBanner } from "./ErrorBanner";

interface QaEntry {
  question: string;
  answer: string;
}

interface AssistantPanelProps {
  deviceId: string;
  ref?: React.Ref<HTMLElement>;
}

export function AssistantPanel({ deviceId, ref }: AssistantPanelProps) {
  const [question, setQuestion] = useState("");
  const [history, setHistory] = useState<QaEntry[]>([]);
  const [conversationId, setConversationId] = useState(() => crypto.randomUUID());
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function startNewConversation() {
    clearConversation(conversationId);
    setConversationId(crypto.randomUUID());
    setHistory([]);
    setError(null);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = question.trim();
    if (!trimmed || loading) return;

    setLoading(true);
    setError(null);
    try {
      const answer = await askAssistant(trimmed, deviceId, conversationId);
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
    <section className="assistant-panel" ref={ref}>
      <div className="assistant-panel__header">
        <h2>
          <ChatCircleDots size={20} weight="bold" />
          Ask the safety assistant
        </h2>
        {history.length > 0 && (
          <button type="button" onClick={startNewConversation} disabled={loading}>
            <ArrowCounterClockwise size={14} />
            New conversation
          </button>
        )}
      </div>

      {history.length === 0 && !loading && (
        <p className="empty-state">
          Try: "Is this device driving safely?" then follow up with "what about cornering?". It remembers the
          conversation.
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

      {error && <ErrorBanner message={error} />}

      <form className="assistant-form" onSubmit={handleSubmit}>
        <input
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="Ask about this device's safety data…"
          aria-label="Question for the assistant"
          disabled={loading}
        />
        <button type="submit" disabled={loading || !question.trim()}>
          <PaperPlaneTilt size={15} />
          {loading ? "Asking…" : "Ask"}
        </button>
      </form>
    </section>
  );
}
