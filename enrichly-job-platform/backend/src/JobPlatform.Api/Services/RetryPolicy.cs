namespace JobPlatform.Api.Services;

public static class RetryPolicy
{
    /// <summary>
    /// Exponential backoff with a small amount of jitter to avoid many failed jobs retrying
    /// in perfect lockstep ("thundering herd"). attemptNumber is the attempt that just failed
    /// (1-based), so the delay before attempt 2 is baseDelay, before attempt 3 is 2x, etc.
    /// Capped at 1 hour so a misconfigured job doesn't end up waiting for a day.
    /// </summary>
    public static TimeSpan GetBackoffDelay(int attemptNumber, int baseDelaySeconds, Random? random = null)
    {
        if (attemptNumber < 1) attemptNumber = 1;
        random ??= Random.Shared;

        var exponent = attemptNumber - 1;
        var rawSeconds = baseDelaySeconds * Math.Pow(2, exponent);
        var cappedSeconds = Math.Min(rawSeconds, TimeSpan.FromHours(1).TotalSeconds);

        // +/- 20% jitter
        var jitterFactor = 0.8 + random.NextDouble() * 0.4;
        var finalSeconds = cappedSeconds * jitterFactor;

        return TimeSpan.FromSeconds(finalSeconds);
    }

    public static bool ShouldRetry(int attemptNumber, int maxAttempts) => attemptNumber < maxAttempts;
}
