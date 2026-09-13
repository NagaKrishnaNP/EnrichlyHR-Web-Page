using JobPlatform.Api.Models.Entities;

namespace JobPlatform.Api.Models.Dto;

public record ExecutionResponse(
    Guid Id,
    Guid JobId,
    string JobName,
    Guid ExecutionGroupId,
    ExecutionStatus Status,
    TriggerType TriggerType,
    int AttemptNumber,
    int MaxAttempts,
    DateTime ScheduledFor,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int? DurationMs,
    int? ResponseStatusCode,
    string? Output,
    string? ErrorMessage,
    string? WorkerId,
    DateTime CreatedAt
);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
