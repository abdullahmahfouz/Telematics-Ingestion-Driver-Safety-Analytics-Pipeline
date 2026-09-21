using TelematicsPipeline.Api.Auth;
using Xunit;

namespace TelematicsPipeline.Api.Tests.Auth;

public class LoginRateLimiterTests
{
    [Fact]
    public void AllowsAttemptsUpToTheCap()
    {
        var limiter = new LoginRateLimiter(maxAttempts: 3);

        Assert.True(limiter.TryAcquire("1.2.3.4").Allowed);
        Assert.True(limiter.TryAcquire("1.2.3.4").Allowed);
        Assert.True(limiter.TryAcquire("1.2.3.4").Allowed);
    }

    [Fact]
    public void BlocksOnceTheCapIsReached()
    {
        var limiter = new LoginRateLimiter(maxAttempts: 2);
        limiter.TryAcquire("1.2.3.4");
        limiter.TryAcquire("1.2.3.4");

        var (allowed, retryAfter) = limiter.TryAcquire("1.2.3.4");

        Assert.False(allowed);
        Assert.True(retryAfter > TimeSpan.Zero);
    }

    [Fact]
    public void TracksEachKeyIndependently()
    {
        var limiter = new LoginRateLimiter(maxAttempts: 1);
        limiter.TryAcquire("1.2.3.4");

        // A different source must not be punished by another source's attempts.
        Assert.True(limiter.TryAcquire("5.6.7.8").Allowed);
    }

    [Fact]
    public void ForgetsAttemptsOnceTheWindowElapses()
    {
        var limiter = new LoginRateLimiter(maxAttempts: 1, window: TimeSpan.FromMilliseconds(50));
        limiter.TryAcquire("1.2.3.4");
        Assert.False(limiter.TryAcquire("1.2.3.4").Allowed);

        Thread.Sleep(75);

        Assert.True(limiter.TryAcquire("1.2.3.4").Allowed);
    }
}
