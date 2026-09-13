using JobPlatform.Api.Data;
using JobPlatform.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Api.Services;

/// <summary>
/// Ticks every few seconds looking for enabled Interval jobs whose NextRunAt has passed, and
/// creates a Pending JobExecution for each one. Safe to run in every API instance at once:
///
///  - Advancing NextRunAt and inserting the execution happen in the same SaveChangesAsync call,
///    so they commit as a single transaction.
///  - Job.RowVersion is an EF concurrency token. If two instances both load the same job on the
///    same tick, only the first SaveChangesAsync wins; the second throws
///    DbUpdateConcurrencyException, which we catch and skip (someone else already scheduled it).
///  - As a second line of defense, (JobId, ScheduledFor) has a unique DB index, so even a race
///    this code doesn't anticipate can only ever produce a duplicate-key error, never a
///    duplicate row.
/// </summary>
public class JobSchedulerService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobSchedulerService> _logger;

    public JobSchedulerService(IServiceScopeFactory scopeFactory, ILogger<JobSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger startup slightly so a fleet of instances doesn't all hit the DB at t=0.
        await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(0, 3)), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScheduleDueJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduler tick failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ScheduleDueJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        var dueJobs = await db.Jobs
            .Where(j => j.IsEnabled && j.ScheduleType == ScheduleType.Interval && j.NextRunAt != null && j.NextRunAt <= now)
            .Take(100)
            .ToListAsync(ct);

        foreach (var job in dueJobs)
        {
            var dueAt = job.NextRunAt!.Value;
            var next = dueAt.AddSeconds(job.IntervalSeconds ?? 60);
            // If we're badly behind (service was down, etc.) don't create a flood of catch-up
            // executions - just resume from "now" so the job doesn't spam once it's back online.
            if (next <= now) next = now.AddSeconds(job.IntervalSeconds ?? 60);
            job.NextRunAt = next;

            db.JobExecutions.Add(new JobExecution
            {
                JobId = job.Id,
                Status = ExecutionStatus.Pending,
                TriggerType = TriggerType.Scheduled,
                AttemptNumber = 1,
                MaxAttempts = job.MaxAttempts,
                ScheduledFor = dueAt,
                IdempotencyKey = $"sched:{job.Id}:{dueAt:O}"
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogDebug("Job {JobId} tick at {DueAt} already scheduled by another instance", job.Id, dueAt);
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex)
            {
                // Most likely the (JobId, ScheduledFor) unique index rejecting a duplicate tick.
                _logger.LogDebug(ex, "Duplicate scheduling attempt for job {JobId} at {DueAt}", job.Id, dueAt);
                db.ChangeTracker.Clear();
            }
        }
    }
}
