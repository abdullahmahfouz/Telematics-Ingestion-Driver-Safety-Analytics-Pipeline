import { useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import { ArrowCounterClockwise, PaperPlaneRight, Sparkle } from "@phosphor-icons/react";
import { askAssistant, AssistantRateLimitError, clearConversation } from "../api/assistantApi";
import { SessionExpiredError } from "../api/authToken";
import { ErrorBanner } from "./ErrorBanner";

interface QaEntry {
  question: string;
  answer: string;
}

interface AssistantPanelProps {
  deviceId: string;
  onSessionExpired: () => void;
  ref?: React.Ref<HTMLElement>;
}

const SUGGESTED_QUESTIONS = [
  "Is this device driving safely?",
  "Summarize harsh events today",
  "How does this compare to yesterday?",
];

export function AssistantPanel({ deviceId, onSessionExpired, ref }: AssistantPanelProps) {
  const [question, setQuestion] = useState("");
  // What's actually in flight, shown next to the loading indicator. Separate from
  // `question` because a suggestion chip submits text that was never typed into the
  // input, so the input's own state can't be relied on to say what's being asked.
  const [pendingQuestion, setPendingQuestion] = useState("");
  const [history, setHistory] = useState<QaEntry[]>([]);
  const [conversationId, setConversationId] = useState(() => crypto.randomUUID());
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Synchronous guard, checked before any state update: `disabled={loading}` alone
  // has a race window between a fast click and React committing the re-render.
  const loadingRef = useRef(false);

  function startNewConversation() {
    clearConversation(conversationId);
    setConversationId(crypto.randomUUID());
    setHistory([]);
    setError(null);
  }

  async function submitQuestion(text: string) {
    const trimmed = text.trim();
    if (!trimmed || loadingRef.current) return;

    loadingRef.current = true;
    setLoading(true);
    setPendingQuestion(trimmed);
    setError(null);
    try {
      const answer = await askAssistant(trimmed, deviceId, conversationId);
      setHistory((prev) => [...prev, { question: trimmed, answer }]);
      setQuestion("");
    } catch (err) {
      if (err instanceof SessionExpiredError) {
        onSessionExpired();
        return;
      }
      if (err instanceof AssistantRateLimitError) {
        setError(err.message);
      } else if (err instanceof TypeError) {
        // fetch() itself throws a TypeError when the request never reached a server at
        // all (connection refused, DNS failure) -- that's the one case where "is it
        // running?" is the right question. Any other Error already carries a specific
        // message from askAssistant (e.g. the backend's own failure detail).
        setError("Could not reach the assistant. Is it running at http://localhost:8000?");
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Could not reach the assistant. Is it running at http://localhost:8000?");
      }
    } finally {
      loadingRef.current = false;
      setLoading(false);
    }
  }

  return (
    <section className="assistant-panel" ref={ref}>
      <div className="assistant-panel__header">
        <h2>
          <Sparkle size={18} weight="fill" />
          Safety assistant
        </h2>
        {history.length > 0 && (
          <button type="button" onClick={startNewConversation} disabled={loading}>
            <ArrowCounterClockwise size={14} />
            New conversation
          </button>
        )}
      </div>

      {history.length === 0 && !loading && (
        <div className="assistant-suggestions">
          <p className="assistant-suggestions__label">Ask about {deviceId}'s safety record</p>
          <div className="assistant-suggestions__chips">
            {SUGGESTED_QUESTIONS.map((suggestion) => (
              <button type="button" key={suggestion} onClick={() => submitQuestion(suggestion)}>
                {suggestion}
              </button>
            ))}
          </div>
        </div>
      )}

      {(history.length > 0 || loading) && (
        <div className="assistant-history">
          {history.map((entry, i) => (
            <div className="assistant-entry" key={i}>
              <p className="assistant-entry__question">{entry.question}</p>
              <div className="assistant-entry__answer">
                <ReactMarkdown>{entry.answer}</ReactMarkdown>
              </div>
            </div>
          ))}
          {loading && (
            <div className="assistant-entry">
              <p className="assistant-entry__question">{pendingQuestion}</p>
              <div className="assistant-thinking" role="status" aria-label="Assistant is thinking">
                <span />
                <span />
                <span />
              </div>
            </div>
          )}
        </div>
      )}

      {error && <ErrorBanner message={error} />}

      <form
        className="assistant-form"
        onSubmit={(e) => {
          e.preventDefault();
          submitQuestion(question);
        }}
      >
        <div className="assistant-form__input-wrap">
          <input
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            placeholder="Ask a question…"
            aria-label="Question for the assistant"
            disabled={loading}
          />
          <button type="submit" disabled={loading || !question.trim()} aria-label="Send">
            <PaperPlaneRight size={16} weight="fill" />
          </button>
        </div>
      </form>
    </section>
  );
}
