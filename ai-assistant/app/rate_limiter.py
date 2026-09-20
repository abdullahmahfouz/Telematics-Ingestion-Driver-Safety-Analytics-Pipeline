import time
from collections import deque


class SlidingWindowRateLimiter:
    """In-memory sliding-window limiter. Fine for a single-process local service --
    if this ever runs behind multiple workers or instances, the limit would need
    to move to a shared store (e.g. Redis) instead, since each process would
    otherwise track its own separate window."""

    def __init__(self, max_requests: int, window_seconds: float) -> None:
        self._max_requests = max_requests
        self._window_seconds = window_seconds
        self._timestamps: deque[float] = deque()

    def try_acquire(self) -> tuple[bool, float]:
        """Returns (allowed, retry_after_seconds). Drops timestamps older than
        the window, then allows the request only if under the cap."""
        now = time.monotonic()
        cutoff = now - self._window_seconds

        while self._timestamps and self._timestamps[0] < cutoff:
            self._timestamps.popleft()

        if len(self._timestamps) >= self._max_requests:
            retry_after = self._window_seconds - (now - self._timestamps[0])
            return False, max(retry_after, 0.0)

        self._timestamps.append(now)
        return True, 0.0


# Gemini's free tier caps gemini-3.8-flash at 5 requests/minute -- we cap
# ourselves at 4/minute so we fail fast with a clear message instead of
# letting a request through only to have Gemini itself reject it.
assistant_rate_limiter = SlidingWindowRateLimiter(max_requests=4, window_seconds=60.0)
