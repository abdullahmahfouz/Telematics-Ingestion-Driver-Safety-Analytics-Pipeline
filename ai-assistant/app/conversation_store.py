from collections import OrderedDict


class ConversationStore:
    """In-memory conversation history, keyed by a client-supplied conversation id.

    Only the conversational turns (user question, assistant answer) are kept --
    not the intermediate tool-call plumbing. Each new question runs its own fresh
    tool-calling round trip; replaying stale tool_call_ids across turns would be
    brittle and buys nothing, since the prior answers already carry the context.

    Evicts the oldest conversation past `max_conversations` so a long-running
    process can't grow unbounded. Not durable -- a restart clears everything,
    which is fine for this stage; persisting would mean moving this to Postgres.
    """

    def __init__(self, max_conversations: int = 100, max_turns: int = 20) -> None:
        self._max_conversations = max_conversations
        self._max_turns = max_turns
        self._conversations: OrderedDict[str, list[dict]] = OrderedDict()

    def get_messages(self, conversation_id: str) -> list[dict]:
        messages = self._conversations.get(conversation_id)
        if messages is None:
            return []
        self._conversations.move_to_end(conversation_id)
        return list(messages)

    def save_messages(self, conversation_id: str, messages: list[dict]) -> None:
        # Keep only the most recent turns so the prompt doesn't grow without limit.
        self._conversations[conversation_id] = messages[-self._max_turns :]
        self._conversations.move_to_end(conversation_id)

        while len(self._conversations) > self._max_conversations:
            self._conversations.popitem(last=False)

    def clear(self, conversation_id: str) -> bool:
        return self._conversations.pop(conversation_id, None) is not None


conversation_store = ConversationStore()
