using JobPlatform.Api.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Job>(entity =>
        {
            entity.HasIndex(j => j.UserId);
            // Scheduler polls "which enabled interval jobs are due" constantly - this index
            // keeps that query cheap even with a large jobs table.
            entity.HasIndex(j => new { j.IsEnabled, j.ScheduleType, j.NextRunAt });

            entity.Property(j => j.RowVersion).IsConcurrencyToken();

            entity.HasOne(j => j.User)
                  .WithMany(u => u.Jobs)
                  .HasForeignKey(j => j.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobExecution>(entity =>
        {
            // Prevents the scheduler from ever creating two executions for the same job at
            // the same due time, even if two API/worker instances race on the same tick.
            entity.HasIndex(e => new { e.JobId, e.ScheduledFor }).IsUnique();

            // Prevents duplicate executions from a double-clicked "Run Now" or a retried HTTP
            // request from a flaky client.
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();

            // Used by the worker's claim query: "give me Pending rows that are due".
            entity.HasIndex(e => new { e.Status, e.ScheduledFor });

            entity.HasIndex(e => e.ExecutionGroupId);
            entity.HasIndex(e => e.JobId);

            entity.Property(e => e.RowVersion).IsConcurrencyToken();
            entity.Property(e => e.Output).HasColumnType("text");
            entity.Property(e => e.ErrorMessage).HasColumnType("text");

            entity.HasOne(e => e.Job)
                  .WithMany(j => j.Executions)
                  .HasForeignKey(e => e.JobId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override int SaveChanges()
    {
        BumpVersions();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        BumpVersions();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// MySQL has no built-in rowversion/timestamp column type like SQL Server, so optimistic
    /// concurrency is implemented by hand: every modified Job/JobExecution gets its RowVersion
    /// incremented here, and EF's concurrency-token check (original value must still match what's
    /// in the DB) does the rest, throwing DbUpdateConcurrencyException on a lost race.
    /// </summary>
    private void BumpVersions()
    {
        foreach (var entry in ChangeTracker.Entries<Job>())
        {
            if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Modified)
            {
                entry.Entity.RowVersion++;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<JobExecution>())
        {
            if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Modified)
            {
                entry.Entity.RowVersion++;
            }
        }
    }
}
