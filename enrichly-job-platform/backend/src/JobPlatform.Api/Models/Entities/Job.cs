using System.ComponentModel.DataAnnotations;

namespace JobPlatform.Api.Models.Entities;

/// <summary>
/// A user-defined automation job. In this build, every job's "action" is an outbound
/// HTTP call (covers "call an API", "trigger a webhook", "sync data" use cases from the
/// brief). The action is intentionally generic (method + url + headers + body) rather than
/// modeled per-use-case, since that keeps the execution engine simple while still covering
/// the example scenarios in the assignment.
/// </summary>
public class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    // --- Action definition (HTTP call) ---
    [MaxLength(10)]
    public string HttpMethod { get; set; } = "GET";

    [MaxLength(2000)]
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>JSON-encoded dictionary of request headers.</summary>
    public string? HeadersJson { get; set; }

    public string? Body { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    // --- Scheduling ---
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Manual;

    /// <summary>Required when ScheduleType is Interval. Minimum enforced at the API layer.</summary>
    public int? IntervalSeconds { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Next time the scheduler should create a Pending execution for this job. Null for Manual jobs.</summary>
    public DateTime? NextRunAt { get; set; }

    // --- Retry policy ---
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Base delay for exponential backoff between retry attempts.</summary>
    public int RetryBaseDelaySeconds { get; set; } = 30;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optimistic concurrency token. Incremented on every update. See ENGINEERING.md.</summary>
    public int RowVersion { get; set; }

    public ICollection<JobExecution> Executions { get; set; } = new List<JobExecution>();
}
