using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using JobPlatform.Api.Controllers;
using JobPlatform.Api.Data;
using JobPlatform.Api.Models.Dto;
using JobPlatform.Api.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobPlatform.Tests;

public class JobsControllerTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static JobsController NewController(AppDbContext db, Guid userId)
    {
        var controller = new JobsController(db, NullLogger<JobsController>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    [Fact]
    public async Task Run_CalledTwiceWithSameIdempotencyKey_ReturnsSameExecution()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var job = new Job { UserId = userId, Name = "Ping", TargetUrl = "https://example.com", HttpMethod = "GET", MaxAttempts = 3 };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var controller = NewController(db, userId);
        var request = new RunJobRequest("double-click-guard");

        var first = Assert.IsType<OkObjectResult>((await controller.Run(job.Id, request)).Result);
        var firstBody = Assert.IsType<ExecutionResponse>(first.Value);

        var second = Assert.IsType<OkObjectResult>((await controller.Run(job.Id, request)).Result);
        var secondBody = Assert.IsType<ExecutionResponse>(second.Value);

        Assert.Equal(firstBody.Id, secondBody.Id);
        Assert.Single(db.JobExecutions);
    }

    [Fact]
    public async Task Run_CalledWithDifferentIdempotencyKeys_CreatesSeparateExecutions()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var job = new Job { UserId = userId, Name = "Ping", TargetUrl = "https://example.com", HttpMethod = "GET", MaxAttempts = 3 };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var controller = NewController(db, userId);

        await controller.Run(job.Id, new RunJobRequest("key-1"));
        await controller.Run(job.Id, new RunJobRequest("key-2"));

        Assert.Equal(2, await db.JobExecutions.CountAsync());
    }

    [Fact]
    public async Task Get_JobOwnedByAnotherUser_ReturnsNotFound()
    {
        await using var db = NewDb();
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var job = new Job { UserId = ownerId, Name = "Private job", TargetUrl = "https://example.com", HttpMethod = "GET" };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var controller = NewController(db, attackerId);
        var result = await controller.Get(job.Id);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_WithStaleRowVersion_ReturnsConflict()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var job = new Job { UserId = userId, Name = "Original", TargetUrl = "https://example.com", HttpMethod = "GET", RowVersion = 1 };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        // Simulate someone else having already updated the job (RowVersion moved to 2 in the DB).
        job.Name = "Changed elsewhere";
        await db.SaveChangesAsync();
        Assert.Equal(2, job.RowVersion);

        var controller = NewController(db, userId);
        var staleRequest = new UpdateJobRequest(
            Name: "My update", Description: null, HttpMethod: "GET", TargetUrl: "https://example.com",
            Headers: null, Body: null, TimeoutSeconds: 30, ScheduleType: ScheduleType.Manual,
            IntervalSeconds: null, IsEnabled: true, MaxAttempts: 3, RetryBaseDelaySeconds: 30,
            RowVersion: 1 // stale - the real current value is 2
        );

        var result = await controller.Update(job.Id, staleRequest);
        Assert.IsType<ConflictObjectResult>(result.Result);
    }
}
