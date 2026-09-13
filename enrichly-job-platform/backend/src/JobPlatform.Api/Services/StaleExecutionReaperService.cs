using JobPlatform.Api.Data;
using JobPlatform.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Api.Services;

/// <summary>
/// Answers "what happens when a worker crashes while processing a job": if a worker dies
/// mid-execution, the row is left stuck in Running forever with no one left to finish it. This
/// service periodically looks for Running executions that have been running far longer than
/// their job's own timeout would allow, and treats them the same as a failed attempt - either
/// requeuing a retry or marking the job permanently failed once attempts are exhausted.
///
/// A generous multiplier (3x the job's timeout, minimum 60s) is used instead of the timeout
/// itself, to avoid racing a worker that is still legitimately mid-request. RowVersion
/// concurrency protects the small remaining race window: if the original worker finishes
/// (success or failure) at the same moment the reaper decides the row is stale, whichever
/// write lands second simply fails its concurrency check and is skipped.
/// </summary>
public class StaleExecutionReaperService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaleExecutionReaperService> _logger;

    public StaleExecutionReaperService(IServiceScopeFactory scopeFactory, ILogger<StaleExecutionReaperService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReapStaleExecutionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stale execution reaper tick failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ReapStaleExecutionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var runningExecutions = await db.JobExecutions
            .Include(e => e.Job)
            .Where(e => e.Status == ExecutionStatus.Running && e.StartedAt != null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var execution in runningExecutions)
        {
            if (execution.Job is null) continue;

            var leaseSeconds = Math.Max(execution.Job.TimeoutSeconds * 3, 60);
            if (now - execution.StartedAt!.Value < TimeSpan.FromSeconds(leaseSeconds)) continue;

            _logger.LogWarning("Reaping stale execution {ExecutionId} for job {JobId} - stuck in Running for over {Seconds}s",
                execution.Id, execution.JobId, leaseSeconds);

            execution.Status = ExecutionStatus.Failed;
            execution.CompletedAt = now;
            execution.ErrorMessage = "Execution timed out without completing - the worker likely crashed or lost connectivity.";

            if (RetryPolicy.ShouldRetry(execution.AttemptNumber, execution.MaxAttempts))
            {
                var delay = RetryPolicy.GetBackoffDelay(execution.AttemptNumber, execution.Job.RetryBaseDelaySeconds);
                db.JobExecutions.Add(new JobExecution
                {
                    JobId = execution.JobId,
                    ExecutionGroupId = execution.ExecutionGroupId,
                    Status = ExecutionStatus.Pending,
                    TriggerType = TriggerType.Retry,
                    AttemptNumber = execution.AttemptNumber + 1,
                    MaxAttempts = execution.MaxAttempts,
                    ScheduledFor = now.Add(delay),
                    IdempotencyKey = $"retry:{execution.ExecutionGroupId}:{execution.AttemptNumber + 1}"
                });
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // The original worker finished (or another reaper pass already reaped it) right
                // as we tried to - safe to ignore, the row is no longer stale either way.
                db.ChangeTracker.Clear();
            }
        }
    }
}
