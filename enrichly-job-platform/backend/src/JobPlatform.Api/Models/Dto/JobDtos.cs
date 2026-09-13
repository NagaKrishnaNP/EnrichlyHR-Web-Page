using System.ComponentModel.DataAnnotations;
using JobPlatform.Api.Models.Entities;

namespace JobPlatform.Api.Models.Dto;

public record CreateJobRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: MaxLength(1000)] string? Description,
    [property: Required, MaxLength(10)] string HttpMethod,
    [property: Required, MaxLength(2000), Url] string TargetUrl,
    Dictionary<string, string>? Headers,
    string? Body,
    [property: Range(1, 300)] int TimeoutSeconds,
    [property: Required] ScheduleType ScheduleType,
    [property: Range(10, 86400)] int? IntervalSeconds,
    [property: Range(1, 10)] int MaxAttempts,
    [property: Range(1, 3600)] int RetryBaseDelaySeconds
);

public record UpdateJobRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: MaxLength(1000)] string? Description,
    [property: Required, MaxLength(10)] string HttpMethod,
    [property: Required, MaxLength(2000), Url] string TargetUrl,
    Dictionary<string, string>? Headers,
    string? Body,
    [property: Range(1, 300)] int TimeoutSeconds,
    [property: Required] ScheduleType ScheduleType,
    [property: Range(10, 86400)] int? IntervalSeconds,
    bool IsEnabled,
    [property: Range(1, 10)] int MaxAttempts,
    [property: Range(1, 3600)] int RetryBaseDelaySeconds,
    [property: Required] int RowVersion
);

public record JobResponse(
    Guid Id,
    string Name,
    string? Description,
    string HttpMethod,
    string TargetUrl,
    Dictionary<string, string>? Headers,
    string? Body,
    int TimeoutSeconds,
    ScheduleType ScheduleType,
    int? IntervalSeconds,
    bool IsEnabled,
    DateTime? NextRunAt,
    int MaxAttempts,
    int RetryBaseDelaySeconds,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int RowVersion,
    JobStatsResponse Stats
);

public record JobStatsResponse(
    int TotalExecutions,
    int SucceededCount,
    int FailedCount,
    DateTime? LastRunAt,
    ExecutionStatus? LastStatus
);

public record RunJobRequest(string? IdempotencyKey);
