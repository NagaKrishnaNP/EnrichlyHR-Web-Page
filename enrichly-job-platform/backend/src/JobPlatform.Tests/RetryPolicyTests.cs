using JobPlatform.Api.Services;
using Xunit;

namespace JobPlatform.Tests;

public class RetryPolicyTests
{
    [Fact]
    public void GetBackoffDelay_GrowsExponentially()
    {
        // Fixed random with no jitter noise by pinning the seed and averaging out; instead we
        // just assert ordering, which jitter (+/-20%) cannot violate across a doubling.
        var deterministicRandom = new Random(42);

        var attempt1 = RetryPolicy.GetBackoffDelay(1, baseDelaySeconds: 30, deterministicRandom);
        var attempt2 = RetryPolicy.GetBackoffDelay(2, baseDelaySeconds: 30, deterministicRandom);
        var attempt3 = RetryPolicy.GetBackoffDelay(3, baseDelaySeconds: 30, deterministicRandom);

        Assert.True(attempt2 > attempt1, "attempt 2 delay should be greater than attempt 1");
        Assert.True(attempt3 > attempt2, "attempt 3 delay should be greater than attempt 2");
    }

    [Fact]
    public void GetBackoffDelay_IsCappedAtOneHour()
    {
        var random = new Random(1);
        // A huge attempt number with a large base delay would otherwise blow past any sane wait.
        var delay = RetryPolicy.GetBackoffDelay(20, baseDelaySeconds: 3600, random);

        Assert.True(delay <= TimeSpan.FromHours(1) * 1.2, "capped delay should never exceed the cap plus jitter margin");
    }

    [Fact]
    public void GetBackoffDelay_NeverNegativeOrZeroForValidInput()
    {
        var random = new Random(7);
        var delay = RetryPolicy.GetBackoffDelay(1, baseDelaySeconds: 5, random);

        Assert.True(delay > TimeSpan.Zero);
    }

    [Theory]
    [InlineData(1, 3, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 3, false)]
    [InlineData(5, 3, false)]
    public void ShouldRetry_RespectsMaxAttempts(int attemptNumber, int maxAttempts, bool expected)
    {
        Assert.Equal(expected, RetryPolicy.ShouldRetry(attemptNumber, maxAttempts));
    }
}
