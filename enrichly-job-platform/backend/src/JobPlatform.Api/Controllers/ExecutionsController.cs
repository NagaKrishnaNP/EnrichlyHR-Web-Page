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
[Route("api/executions")]
public class ExecutionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExecutionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ExecutionResponse>>> List(
        [FromQuery] Guid? jobId,
        [FromQuery] ExecutionStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var userId = User.GetUserId();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.JobExecutions
            .Include(e => e.Job)
            .Where(e => e.Job!.UserId == userId);

        if (jobId.HasValue) query = query.Where(e => e.JobId == jobId.Value);
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var response = items.Select(e => ToResponse(e, e.Job!.Name)).ToList();
        return Ok(new PagedResult<ExecutionResponse>(response, total, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExecutionResponse>> Get(Guid id)
    {
        var userId = User.GetUserId();
        var execution = await _db.JobExecutions.Include(e => e.Job)
            .FirstOrDefaultAsync(e => e.Id == id && e.Job!.UserId == userId);
        if (execution is null) return NotFound();

        return Ok(ToResponse(execution, execution.Job!.Name));
    }

    /// <summary>Cancel an execution that hasn't started yet. Once a worker claims it (Running), it's too late to cancel.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ExecutionResponse>> Cancel(Guid id)
    {
        var userId = User.GetUserId();
        var execution = await _db.JobExecutions.Include(e => e.Job)
            .FirstOrDefaultAsync(e => e.Id == id && e.Job!.UserId == userId);
        if (execution is null) return NotFound();

        if (execution.Status != ExecutionStatus.Pending)
        {
            return BadRequest(new { message = $"Only Pending executions can be cancelled (this one is {execution.Status})." });
        }

        execution.Status = ExecutionStatus.Cancelled;
        execution.CompletedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "A worker just claimed this execution - it can no longer be cancelled." });
        }

        return Ok(ToResponse(execution, execution.Job!.Name));
    }

    /// <summary>
    /// Manually retry a terminally-Failed execution. Starts a brand new attempt chain (its own
    /// ExecutionGroupId, AttemptNumber back to 1) rather than reusing the exhausted one, since
    /// automatic retries for the original run are done - this is the user explicitly asking to
    /// try again, so it gets a full fresh set of attempts.
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<ExecutionResponse>> Retry(Guid id)
    {
        var userId = User.GetUserId();
        var execution = await _db.JobExecutions.Include(e => e.Job)
            .FirstOrDefaultAsync(e => e.Id == id && e.Job!.UserId == userId);
        if (execution is null) return NotFound();

        if (execution.Status != ExecutionStatus.Failed)
        {
            return BadRequest(new { message = "Only Failed executions can be retried." });
        }

        var job = execution.Job!;
        var newExecution = new JobExecution
        {
            JobId = job.Id,
            Status = ExecutionStatus.Pending,
            TriggerType = TriggerType.Manual,
            AttemptNumber = 1,
            MaxAttempts = job.MaxAttempts,
            ScheduledFor = DateTime.UtcNow,
            IdempotencyKey = $"manual-retry:{id}:{Guid.NewGuid()}"
        };

        _db.JobExecutions.Add(newExecution);
        await _db.SaveChangesAsync();

        return Ok(ToResponse(newExecution, job.Name));
    }

    private static ExecutionResponse ToResponse(JobExecution e, string jobName) => new(
        e.Id, e.JobId, jobName, e.ExecutionGroupId, e.Status, e.TriggerType, e.AttemptNumber, e.MaxAttempts,
        e.ScheduledFor, e.StartedAt, e.CompletedAt, e.DurationMs, e.ResponseStatusCode, e.Output, e.ErrorMessage,
        e.WorkerId, e.CreatedAt
    );
}
