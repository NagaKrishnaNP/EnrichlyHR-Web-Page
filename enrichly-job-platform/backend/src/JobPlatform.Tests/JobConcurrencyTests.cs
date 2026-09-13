using JobPlatform.Api.Data;
using JobPlatform.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobPlatform.Tests;

/// <summary>
/// Verifies the optimistic-concurrency story described in ENGINEERING.md: "two requests try to
/// update the same job at the same time" should result in one of them being told to retry,
/// never in one silently overwriting the other's change.
/// </summary>
public class JobConcurrencyTests
{
    private static DbContextOptions<AppDbContext> NewInMemoryOptions(string dbName) =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;

    [Fact]
    public async Task ConcurrentUpdates_ToSameJob_SecondWriterFailsConcurrencyCheck()
    {
        var dbName = Guid.NewGuid().ToString();
        var jobId = Guid.NewGuid();

        await using (var seedDb = new AppDbContext(NewInMemoryOptions(dbName)))
        {
            seedDb.Jobs.Add(new Job
            {
                Id = jobId,
                UserId = Guid.NewGuid(),
                Name = "Original name",
                TargetUrl = "https://example.com",
                HttpMethod = "GET",
                RowVersion = 1
            });
            await seedDb.SaveChangesAsync();
        }

        // Two independent contexts simulate two concurrent HTTP requests, each loading the job
        // before either one has written back its change.
        await using var dbA = new AppDbContext(NewInMemoryOptions(dbName));
        await using var dbB = new AppDbContext(NewInMemoryOptions(dbName));

        var jobA = await dbA.Jobs.FirstAsync(j => j.Id == jobId);
        var jobB = await dbB.Jobs.FirstAsync(j => j.Id == jobId);

        jobA.Name = "Updated by request A";
        await dbA.SaveChangesAsync(); // succeeds, RowVersion is now 2

        jobB.Name = "Updated by request B";

        // request B is still holding RowVersion 1 as its "original value" - the DB now has 2.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    [Fact]
    public async Task SequentialUpdates_ToSameJob_BothSucceed()
    {
        var dbName = Guid.NewGuid().ToString();
        var jobId = Guid.NewGuid();

        await using (var seedDb = new AppDbContext(NewInMemoryOptions(dbName)))
        {
            seedDb.Jobs.Add(new Job
            {
                Id = jobId,
                UserId = Guid.NewGuid(),
                Name = "Original name",
                TargetUrl = "https://example.com",
                HttpMethod = "GET",
                RowVersion = 1
            });
            await seedDb.SaveChangesAsync();
        }

        await using (var db1 = new AppDbContext(NewInMemoryOptions(dbName)))
        {
            var job = await db1.Jobs.FirstAsync(j => j.Id == jobId);
            job.Name = "First update";
            await db1.SaveChangesAsync();
        }

        await using (var db2 = new AppDbContext(NewInMemoryOptions(dbName)))
        {
            var job = await db2.Jobs.FirstAsync(j => j.Id == jobId);
            job.Name = "Second update";
            await db2.SaveChangesAsync(); // loaded the fresh RowVersion, so this is fine
        }

        await using var verifyDb = new AppDbContext(NewInMemoryOptions(dbName));
        var final = await verifyDb.Jobs.FirstAsync(j => j.Id == jobId);
        Assert.Equal("Second update", final.Name);
        Assert.Equal(3, final.RowVersion); // bumped once per successful save
    }
}
