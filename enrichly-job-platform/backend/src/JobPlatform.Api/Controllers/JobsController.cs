using System.Text.Json;
using JobPlatform.Api.Auth;
using JobPlatform.Api.Data;
using JobPlatform.Api.Models.Dto;
using JobPlatform.Api.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/jobs")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<JobsController> _logger;

    public JobsController(AppDbContext db, ILogger<JobsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JobResponse>>> List([FromQuery] string? search, [FromQuery] bool? enabled)
    {
        var userId = User.GetUserId();
        var query = _db.Jobs.Where(j => j.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(j => j.Name.Contains(search) || (j.Description != null && j.Description.Contains(search)));
        }
        if (enabled.HasValue)
        {
            query = query.Where(j => j.IsEnabled == enabled.Value);
        }

        var jobs = await query.OrderByDescending(j => j.CreatedAt).ToListAsync();
        var jobIds = jobs.Select(j => j.Id).ToList();

        // Pulled into memory and grouped with LINQ-to-Objects rather than a GroupBy().Select(g
        // => g.OrderBy...First()) EF query - that "last row per group" shape doesn't reliably
        // translate to SQL across providers, and the per-user job list is small enough that
        // this is simpler and safer than fighting query translation.
        var executions = await _db.JobExecutions
            .AsNoTracking()
            .Where(e => jobIds.Contains(e.JobId))
            .Select(e => new { e.JobId, e.Status, e.CreatedAt })
            .ToListAsync();

                var statsByJob = executions
            .GroupBy(e => e.JobId)
            .ToDictionary(g => g.Key, g => new JobStatsAggregate(
                g.Count(),
                g.Count(e => e.Status == ExecutionStatus.Succeeded),
                g.Count(e => e.Status == ExecutionStatus.Failed),
                g.Max(e => (DateTime?)e.CreatedAt)
            ));

        var lastStatusByJob = executions
            .GroupBy(e => e.JobId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.CreatedAt).First().Status);

        var responses = jobs.Select(j =>
        {
            var lastStatus = lastStatusByJob.TryGetValue(j.Id, out var ls) ? ls : (ExecutionStatus?)null;
            return ToResponse(j, statsByJob.GetValueOrDefault(j.Id), lastStatus);
        });

        return Ok(responses);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobResponse>> Get(Guid id)
    {
        var userId = User.GetUserId();
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);
        if (job is null) return NotFound();

                var executions = await _db.JobExecutions.Where(e => e.JobId == id).ToListAsync();
        var stats = new JobStatsAggregate(
            executions.Count,
            executions.Count(e => e.Status == ExecutionStatus.Succeeded),
            executions.Count(e => e.Status == ExecutionStatus.Failed),
            executions.OrderByDescending(e => e.CreatedAt).FirstOrDefault()?.CreatedAt
        );
        var lastStatus = executions.OrderByDescending(e => e.CreatedAt).FirstOrDefault()?.Status;

        return Ok(ToResponse(job, stats, lastStatus));
    }

    [HttpPost]
    public async Task<ActionResult<JobResponse>> Create(CreateJobRequest request)
    {
        if (request.ScheduleType == ScheduleType.Interval && (request.IntervalSeconds is null || request.IntervalSeconds < 10))
        {
            return BadRequest(new { message = "IntervalSeconds is required (minimum 10) for interval jobs." });
        }

        var userId = User.GetUserId();
        var job = new Job
        {
            UserId = userId,
            Name = request.Name,
            Description = request.Description,
            HttpMethod = request.HttpMethod.ToUpperInvariant(),
            TargetUrl = request.TargetUrl,
            HeadersJson = request.Headers is null ? null : JsonSerializer.Serialize(request.Headers),
            Body = request.Body,
            TimeoutSeconds = request.TimeoutSeconds,
            ScheduleType = request.ScheduleType,
            IntervalSeconds = request.ScheduleType == ScheduleType.Interval ? request.IntervalSeconds : null,
            IsEnabled = true,
            MaxAttempts = request.MaxAttempts,
            RetryBaseDelaySeconds = request.RetryBaseDelaySeconds,
            NextRunAt = request.ScheduleType == ScheduleType.Interval ? DateTime.UtcNow.AddSeconds(request.IntervalSeconds!.Value) : null
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = job.Id }, ToResponse(job, null, null));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JobResponse>> Update(Guid id, UpdateJobRequest request)
    {
        if (request.ScheduleType == ScheduleType.Interval && (request.IntervalSeconds is null || request.IntervalSeconds < 10))
        {
            return BadRequest(new { message = "IntervalSeconds is required (minimum 10) for interval jobs." });
        }

        var userId = User.GetUserId();
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);
        if (job is null) return NotFound();

        // Optimistic concurrency: the client sends back the RowVersion it last saw. If someone
        // else (another tab, another device) updated this job in the meantime, RowVersion here
        // now points at a stale value, EF's SaveChanges will detect the mismatch and throw.
        _db.Entry(job).Property(j => j.RowVersion).OriginalValue = request.RowVersion;

        var wasInterval = job.ScheduleType == ScheduleType.Interval;

        job.Name = request.Name;
        job.Description = request.Description;
        job.HttpMethod = request.HttpMethod.ToUpperInvariant();
        job.TargetUrl = request.TargetUrl;
        job.HeadersJson = request.Headers is null ? null : JsonSerializer.Serialize(request.Headers);
        job.Body = request.Body;
        job.TimeoutSeconds = request.TimeoutSeconds;
        job.ScheduleType = request.ScheduleType;
        job.IntervalSeconds = request.ScheduleType == ScheduleType.Interval ? request.IntervalSeconds : null;
        job.IsEnabled = request.IsEnabled;
        job.MaxAttempts = request.MaxAttempts;
        job.RetryBaseDelaySeconds = request.RetryBaseDelaySeconds;

        if (request.ScheduleType == ScheduleType.Interval && (!wasInterval || job.NextRunAt is null))
        {
            job.NextRunAt = DateTime.UtcNow.AddSeconds(request.IntervalSeconds!.Value);
        }
        else if (request.ScheduleType == ScheduleType.Manual)
        {
            job.NextRunAt = null;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "This job was modified elsewhere. Reload and try again." });
        }

        return Ok(ToResponse(job, null, null));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.GetUserId();
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);
        if (job is null) return NotFound();

        _db.Jobs.Remove(job);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// "Run Now". Idempotent: if the caller supplies (or retries) the same IdempotencyKey - e.g.
    /// because a double-click fired two requests, or a flaky network caused a client-side retry -
    /// we return the existing execution instead of creating a second one.
    /// </summary>
    [HttpPost("{id:guid}/run")]
    public async Task<ActionResult<ExecutionResponse>> Run(Guid id, RunJobRequest? request)
    {
        var userId = User.GetUserId();
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);
        if (job is null) return NotFound();

        var idempotencyKey = string.IsNullOrWhiteSpace(request?.IdempotencyKey)
            ? $"manual:{Guid.NewGuid()}"
            : $"manual:{id}:{request.IdempotencyKey}";

        var existing = await _db.JobExecutions.FirstOrDefaultAsync(e => e.IdempotencyKey == idempotencyKey);
        if (existing is not null)
        {
            return Ok(ToExecutionResponse(existing, job.Name));
        }

        var now = DateTime.UtcNow;
        var execution = new JobExecution
        {
            JobId = job.Id,
            Status = ExecutionStatus.Pending,
            TriggerType = TriggerType.Manual,
            AttemptNumber = 1,
            MaxAttempts = job.MaxAttempts,
            ScheduledFor = now,
            IdempotencyKey = idempotencyKey
        };

        _db.JobExecutions.Add(execution);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race against a duplicate request with the same idempotency key.
            var raced = await _db.JobExecutions.AsNoTracking().FirstOrDefaultAsync(e => e.IdempotencyKey == idempotencyKey);
            if (raced is not null) return Ok(ToExecutionResponse(raced, job.Name));
            throw;
        }

        return Ok(ToExecutionResponse(execution, job.Name));
    }

        private sealed record JobStatsAggregate(int Total, int Succeeded, int Failed, DateTime? LastRunAt);

    private static JobResponse ToResponse(Job job, JobStatsAggregate? stats, ExecutionStatus? lastStatus)
    {
        Dictionary<string, string>? headers = job.HeadersJson is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(job.HeadersJson);

        var statsResponse = new JobStatsResponse
        {
            TotalExecutions = stats?.Total ?? 0,
            SucceededCount = stats?.Succeeded ?? 0,
            FailedCount = stats?.Failed ?? 0,
            LastRunAt = stats?.LastRunAt,
            LastStatus = lastStatus
        };

        return new JobResponse
        {
            Id = job.Id,
            Name = job.Name,
            Description = job.Description,
            HttpMethod = job.HttpMethod,
            TargetUrl = job.TargetUrl,
            Headers = headers,
            Body = job.Body,
            TimeoutSeconds = job.TimeoutSeconds,
            ScheduleType = job.ScheduleType,
            IntervalSeconds = job.IntervalSeconds,
            IsEnabled = job.IsEnabled,
            NextRunAt = job.NextRunAt,
            MaxAttempts = job.MaxAttempts,
            RetryBaseDelaySeconds = job.RetryBaseDelaySeconds,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
            RowVersion = job.RowVersion,
            Stats = statsResponse
        };
    }

    private static ExecutionResponse ToExecutionResponse(JobExecution e, string jobName) => new(
        e.Id, e.JobId, jobName, e.ExecutionGroupId, e.Status, e.TriggerType, e.AttemptNumber, e.MaxAttempts,
        e.ScheduledFor, e.StartedAt, e.CompletedAt, e.DurationMs, e.ResponseStatusCode, e.Output, e.ErrorMessage,
        e.WorkerId, e.CreatedAt
    );
}
