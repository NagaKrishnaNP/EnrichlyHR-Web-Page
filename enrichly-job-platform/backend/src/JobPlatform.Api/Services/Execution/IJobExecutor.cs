using JobPlatform.Api.Models.Entities;

namespace JobPlatform.Api.Services.Execution;

public record ExecutionResult(bool Success, int? StatusCode, string? Output, string? ErrorMessage);

public interface IJobExecutor
{
    Task<ExecutionResult> ExecuteAsync(Job job, CancellationToken cancellationToken);
}
