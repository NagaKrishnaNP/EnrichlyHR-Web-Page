using System.Diagnostics;
using JobPlatform.Api.Data;
using JobPlatform.Api.Models.Entities;
using JobPlatform.Api.Services.Execution;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace JobPlatform.Api.Services;

/// <summary>
/// Polls for Pending executions that are due and runs them. This is the piece that answers
/// "how does the system support multiple workers without double-running a job":
///
///   BEGIN;
///   SELECT id FROM JobExecutions WHERE Status = Pending AND ScheduledFor <= NOW()
///     ORDER BY ScheduledFor LIMIT :batchSize FOR UPDATE SKIP LOCKED;
///   UPDATE JobExecutions SET Status = Running, ... WHERE Id IN (...);
///   COMMIT;
///
/// FOR UPDATE locks the selected rows; SKIP LOCKED means a second worker running the exact same
/// query concurrently simply skips rows already locked by the first worker instead of blocking
/// on them, so N workers can poll in a tight loop and each row is only ever claimed once. The
/// actual HTTP call happens *after* this transaction commits, so we're not holding row locks
/// (or a DB connection) for the whole duration of a slow external request.
///
/// Run more than one instance of the API (see docker-compose.yml, `docker compose up --scale
/// api=3`) to see multiple workers pulling from the same queue.
/// </summary>
public class JobWorkerService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 5;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobWorkerService> _logger;

    public JobWorkerService(IServiceScopeFactory scopeFactory, ILogger<JobWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job worker {WorkerId} starting", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            int claimedCount;
            try
            {
                claimedCount = await ClaimAndRunBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} batch failed", _workerId);
                claimedCount = 0;
            }

            // Back off only when there was nothing to do, so a busy queue is drained quickly.
            if (claimedCount == 0)
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task<int> ClaimAndRunBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var claimedIds = await ClaimBatchAsync(db, ct);
        if (claimedIds.Count == 0) return 0;

        foreach (var id in claimedIds)
        {
            await RunSingleExecutionAsync(scope, id, ct);
        }

        return claimedIds.Count;
    }

    /// <summary>Atomically claims a batch of due Pending executions and flips them to Running.</summary>
    private async Task<List<Guid>> ClaimBatchAsync(AppDbContext db, CancellationToken ct)
    {
        var claimed = new List<Guid>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await using (var selectCmd = (MySqlCommand)connection.CreateCommand())
            {
                selectCmd.Transaction = (MySqlTransaction)transaction;
                selectCmd.CommandText = @"
                    SELECT Id FROM JobExecutions
                    WHERE Status = 0 AND ScheduledFor <= UTC_TIMESTAMP()
                    ORDER BY ScheduledFor
                    LIMIT @batchSize
                    FOR UPDATE SKIP LOCKED;";
                selectCmd.Parameters.AddWithValue("@batchSize", BatchSize);

                await using var reader = await selectCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    claimed.Add(reader.GetGuid(0));
                }
            }

            if (claimed.Count > 0)
            {
                await using var updateCmd = (MySqlCommand)connection.CreateCommand();
                updateCmd.Transaction = (MySqlTransaction)transaction;
                var idParams = claimed.Select((_, i) => $"@id{i}").ToArray();
                updateCmd.CommandText = $@"
                    UPDATE JobExecutions
                    SET Status = 1, StartedAt = UTC_TIMESTAMP(), WorkerId = @workerId, RowVersion = RowVersion + 1
                    WHERE Id IN ({string.Join(",", idParams)});";
                updateCmd.Parameters.AddWithValue("@workerId", _workerId);
                for (int i = 0; i < claimed.Count; i++)
                {
                    updateCmd.Parameters.AddWithValue(idParams[i], claimed[i]);
                }
                await updateCmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return claimed;
    }

    private async Task RunSingleExecutionAsync(IServiceScope scope, Guid executionId, CancellationToken ct)
    {
        var executor = scope.ServiceProvider.GetRequiredService<IJobExecutor>();

        // Fresh load in a new short-lived context per execution so one slow/broken job can't
        // hold up the DbContext used to claim the next batch.
        using var execScope = scope.ServiceProvider.CreateScope();
        var execDb = execScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var execution = await execDb.JobExecutions.Include(e => e.Job).FirstOrDefaultAsync(e => e.Id == executionId, ct);
        if (execution?.Job is null)
        {
            _logger.LogWarning("Claimed execution {ExecutionId} but it (or its job) no longer exists", executionId);
            return;
        }

        var job = execution.Job;
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Worker {WorkerId} running execution {ExecutionId} (job {JobName}, attempt {Attempt}/{Max})",
            _workerId, execution.Id, job.Name, execution.AttemptNumber, execution.MaxAttempts);

        var result = await executor.ExecuteAsync(job, ct);
        stopwatch.Stop();

        execution.CompletedAt = DateTime.UtcNow;
        execution.DurationMs = (int)stopwatch.ElapsedMilliseconds;
        execution.ResponseStatusCode = result.StatusCode;
        execution.Output = result.Output;
        execution.ErrorMessage = result.ErrorMessage;

        if (result.Success)
        {
            execution.Status = ExecutionStatus.Succeeded;
            await execDb.SaveChangesAsync(ct);
            return;
        }

        if (RetryPolicy.ShouldRetry(execution.AttemptNumber, execution.MaxAttempts))
        {
            execution.Status = ExecutionStatus.Failed;
            var delay = RetryPolicy.GetBackoffDelay(execution.AttemptNumber, job.RetryBaseDelaySeconds);
            var nextAttemptAt = DateTime.UtcNow.Add(delay);

            execDb.JobExecutions.Add(new JobExecution
            {
                JobId = job.Id,
                ExecutionGroupId = execution.ExecutionGroupId,
                Status = ExecutionStatus.Pending,
                TriggerType = TriggerType.Retry,
                AttemptNumber = execution.AttemptNumber + 1,
                MaxAttempts = execution.MaxAttempts,
                ScheduledFor = nextAttemptAt,
                IdempotencyKey = $"retry:{execution.ExecutionGroupId}:{execution.AttemptNumber + 1}"
            });

            _logger.LogInformation("Execution {ExecutionId} failed, scheduling attempt {NextAttempt} at {NextAttemptAt}",
                execution.Id, execution.AttemptNumber + 1, nextAttemptAt);
        }
        else
        {
            execution.Status = ExecutionStatus.Failed;
            _logger.LogWarning("Execution {ExecutionId} failed permanently after {Attempts} attempts",
                execution.Id, execution.AttemptNumber);
        }

        await execDb.SaveChangesAsync(ct);
    }
}
