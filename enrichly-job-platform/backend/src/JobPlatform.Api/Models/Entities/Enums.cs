namespace JobPlatform.Api.Models.Entities;

public enum ScheduleType
{
    /// <summary>Job only runs when a user clicks "Run Now" (or calls the trigger API).</summary>
    Manual = 0,

    /// <summary>Job runs automatically every IntervalSeconds.</summary>
    Interval = 1
}

public enum ExecutionStatus
{
    /// <summary>Queued, waiting for a worker to pick it up.</summary>
    Pending = 0,

    /// <summary>Claimed by a worker and currently executing.</summary>
    Running = 1,

    /// <summary>Completed successfully.</summary>
    Succeeded = 2,

    /// <summary>Failed and will not be retried again (either out of attempts, or cancelled).</summary>
    Failed = 3,

    /// <summary>Cancelled by a user before it started running.</summary>
    Cancelled = 4
}

public enum TriggerType
{
    /// <summary>Created automatically by the scheduler because the job's interval elapsed.</summary>
    Scheduled = 0,

    /// <summary>Created because a user clicked "Run Now".</summary>
    Manual = 1,

    /// <summary>Created automatically as a retry of a previous failed attempt.</summary>
    Retry = 2
}
