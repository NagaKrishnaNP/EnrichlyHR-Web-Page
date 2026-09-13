namespace JobPlatform.Api.Models.Entities;

/// <summary>
/// A single attempt to run a job. A logical "run" that gets retried three times produces
/// three JobExecution rows sharing the same ExecutionGroupId, one per attempt - this keeps
/// the state machine trivial (every row only ever moves Pending -> Running -> {Succeeded|Failed})
/// and makes the full attempt history easy to query and display.
/// </summary>
public class JobExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobId { get; set; }
    public Job? Job { get; set; }

    /// <summary>Groups every attempt of the same logical run together.</summary>
    public Guid ExecutionGroupId { get; set; } = Guid.NewGuid();

    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;
    public TriggerType TriggerType { get; set; }

    public int AttemptNumber { get; set; } = 1;
    public int MaxAttempts { get; set; }

    /// <summary>
    /// The time this attempt is due to run. For the first attempt this is "now" (manual) or the
    /// scheduled tick time. For retries it's the backoff time. Combined with JobId, this is
    /// protected by a unique index so the scheduler can never double-insert the same tick even
    /// if two API/worker instances race - see ENGINEERING.md "Duplicate scheduling".
    /// </summary>
    public DateTime ScheduledFor { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? DurationMs { get; set; }

    public int? ResponseStatusCode { get; set; }

    /// <summary>Truncated response body / output, for display in the UI.</summary>
    public string? Output { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Identifies which worker process claimed this execution (host:pid). Useful for debugging stuck jobs.</summary>
    public string? WorkerId { get; set; }

    /// <summary>
    /// Always populated (server-generated if the caller doesn't supply one). Unique per row.
    /// Lets a "Run Now" double-click, or a retried HTTP request from a flaky client, resolve
    /// to the same execution instead of creating two.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int RowVersion { get; set; }
}
