using System.Collections.Concurrent;

namespace TelematicsPipeline.Api.Auth;

/// <summary>
/// In-memory sliding-window limiter on login attempts, keyed by remote IP -- mirrors the AI
/// assistant's own SlidingWindowRateLimiter. It throttles brute-forcing the login endpoint from
/// one source; it does not stop an attack spread across many IPs, and the state is per-process,
/// so it would need to move to a shared store (e.g. Redis) if this API ever ran as more than
/// one instance.
/// </summary>
public sealed class LoginRateLimiter(int maxAttempts = 10, TimeSpan? window = null)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _attemptsByKey = new();

    /// <summary>
    /// Records one attempt for <paramref name="key"/> and returns whether it's allowed. Both
    /// successful and failed logins count toward the cap -- a valid credential used to hammer
    /// the endpoint is still hammering the endpoint.
    /// </summary>
    public (bool Allowed, TimeSpan RetryAfter) TryAcquire(string key)
    {
        var now = DateTimeOffset.UtcNow;
        var attempts = _attemptsByKey.GetOrAdd(key, _ => new Queue<DateTimeOffset>());

        lock (attempts)
        {
            while (attempts.Count > 0 && now - attempts.Peek() >= _window)
            {
                attempts.Dequeue();
            }

            if (attempts.Count >= maxAttempts)
            {
                var retryAfter = _window - (now - attempts.Peek());
                return (false, retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero);
            }

            attempts.Enqueue(now);
            return (true, TimeSpan.Zero);
        }
    }
}
