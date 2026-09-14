using System.ComponentModel.DataAnnotations;

using JobPlatform.Api.Models.Entities;

namespace JobPlatform.Api.Models.Dto;

public class CreateJobRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string HttpMethod { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    [Url]
    public string TargetUrl { get; set; } = string.Empty;

    public Dictionary<string, string>? Headers { get; set; }

    public string? Body { get; set; }

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; }

    [Required]
    public ScheduleType ScheduleType { get; set; }

    [Range(10, 86400)]
    public int? IntervalSeconds { get; set; }

    [Range(1, 10)]
    public int MaxAttempts { get; set; }

    [Range(1, 3600)]
    public int RetryBaseDelaySeconds { get; set; }
}

public class UpdateJobRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(10)]
    public string HttpMethod { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    [Url]
    public string TargetUrl { get; set; } = string.Empty;

    public Dictionary<string, string>? Headers { get; set; }

    public string? Body { get; set; }

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; }

    [Required]
    public ScheduleType ScheduleType { get; set; }

    [Range(10, 86400)]
    public int? IntervalSeconds { get; set; }

    public bool IsEnabled { get; set; }

    [Range(1, 10)]
    public int MaxAttempts { get; set; }

    [Range(1, 3600)]
    public int RetryBaseDelaySeconds { get; set; }

    [Required]
    public int RowVersion { get; set; }
}

public class JobResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string HttpMethod { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public Dictionary<string, string>? Headers { get; set; }

    public string? Body { get; set; }

    public int TimeoutSeconds { get; set; }

    public ScheduleType ScheduleType { get; set; }

    public int? IntervalSeconds { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime? NextRunAt { get; set; }

    public int MaxAttempts { get; set; }

    public int RetryBaseDelaySeconds { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int RowVersion { get; set; }

    public JobStatsResponse Stats { get; set; } = new();
}

public class JobStatsResponse
{
    public int TotalExecutions { get; set; }

    public int SucceededCount { get; set; }

    public int FailedCount { get; set; }

    public DateTime? LastRunAt { get; set; }

    public ExecutionStatus? LastStatus { get; set; }
}

public class RunJobRequest
{
    public string? IdempotencyKey { get; set; }
}
