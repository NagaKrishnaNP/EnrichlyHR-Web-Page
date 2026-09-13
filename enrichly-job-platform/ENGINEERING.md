# Engineering Notes

## Architecture overview

```
                     ┌─────────────┐
   Browser  ───────▶ │  Angular    │
                     │  (nginx)    │
                     └──────┬──────┘
                            │ REST + JWT
                            ▼
                     ┌─────────────┐        every API instance also runs:
                     │  ASP.NET    │◀────┐    - JobSchedulerService (creates due executions)
                     │  Core API   │     │    - JobWorkerService     (claims + runs executions)
                     └──────┬──────┘     │    - StaleExecutionReaperService
                            │            │
                            ▼            │
                     ┌─────────────┐     │
                     │   MySQL 8   │─────┘
                     │ (the queue  │
                     │  lives here)│
                     └─────────────┘
```

There's no separate message broker (RabbitMQ/SQS/etc). `JobExecutions` rows *are* the queue:
"Pending" rows are queued work, and `SELECT ... FOR UPDATE SKIP LOCKED` (below) gives the
multi-worker safety a broker would normally provide, without adding another moving part. This
was a deliberate scope trade-off for an 8-12 hour build - see "What I'd improve" for when I'd
reach for a real broker instead.

Every API instance runs the HTTP API *and* embeds the scheduler/worker/reaper as background
services (`IHostedService`s wired up in `Program.cs`, individually toggleable via the
`Worker__Enable*` config flags - see the README). Running multiple `api` containers
(`docker compose up --scale api=3`) gives you multiple independent workers pulling from the
same queue for free, which is what "the system should support multiple workers" asks for. The
trade-off: API request latency and job execution now share the same process's thread pool. For
this assignment's scope that's an acceptable simplification; a follow-up would split them into
separate deployables (see below).

## How jobs are picked up and executed

Every job's action is an outbound HTTP request (method, URL, headers, body, timeout) - this
covers all four example scenarios in the brief ("call an API", "sync data between two systems"
via an API call, "trigger a webhook", "run a background process" invoked over HTTP), while
keeping the execution engine to one code path instead of a plugin system for N job types. I
judged that a generic, well-executed HTTP action was more valuable in the time available than
several half-implemented action types.

1. **Scheduling.** `JobSchedulerService` polls every 5s for enabled `Interval` jobs whose
   `NextRunAt` has passed, and inserts a `Pending` `JobExecution` row for each one due.
2. **Claiming.** `JobWorkerService` polls every 2s and runs, inside one DB transaction:
   ```sql
   SELECT Id FROM JobExecutions
   WHERE Status = Pending AND ScheduledFor <= NOW()
   ORDER BY ScheduledFor LIMIT :batchSize
   FOR UPDATE SKIP LOCKED;

   UPDATE JobExecutions SET Status = Running, StartedAt = NOW(), WorkerId = :id
   WHERE Id IN (...claimed...);
   ```
   `FOR UPDATE` locks the selected rows; `SKIP LOCKED` means a second worker running the exact
   same query concurrently skips rows the first worker already locked instead of blocking on
   them. N workers can poll in a tight loop and every row is claimed by exactly one of them.
3. **Executing.** The actual HTTP call happens *after* the claim transaction commits, so a slow
   or hanging external API doesn't hold a DB row lock or connection for its whole duration.
4. **Completing.** The worker writes the result back (`Succeeded`/`Failed`, status code, output,
   error, duration) in a normal update, and - if it failed and attempts remain - inserts the
   next attempt as a new `Pending` row scheduled after a backoff delay.

## Concurrency

| Scenario | How it's handled |
|---|---|
| Two workers claim the same execution | `FOR UPDATE SKIP LOCKED` (above) - structurally impossible, not just checked-and-hoped |
| Scheduler runs in two API instances at once | `Job.RowVersion` is an EF concurrency token; whichever instance's `SaveChanges` commits first wins the tick, the second gets `DbUpdateConcurrencyException` and skips. Backstopped by a unique index on `(JobId, ScheduledFor)` - even a bug in the app-level logic can only produce a duplicate-key error, never a duplicate execution. |
| Two browser tabs edit the same job | `UpdateJobRequest.RowVersion` must match the DB's current value or the API returns `409 Conflict` and the UI asks the user to reload (`JobFormComponent.conflict`) |
| User double-clicks "Run Now" | `IdempotencyKey` (unique, server-generated per click) - a repeat with the same key returns the existing execution instead of creating a second one (`JobsControllerTests.Run_CalledTwiceWithSameIdempotencyKey_ReturnsSameExecution`) |
| A worker crashes mid-execution | Row is stuck in `Running`. `StaleExecutionReaperService` finds executions running longer than 3x their job's timeout and requeues them as a retry (or fails them permanently if attempts are exhausted) |
| DB briefly unavailable | `EnableRetryOnFailure` on the EF MySQL provider retries transient connection errors; the worker/scheduler loops just try again next tick rather than crashing the process |
| External API unavailable / times out / connection drops | Caught in `HttpJobExecutor` and recorded as a normal failed attempt, subject to the same retry policy as any other failure - a flaky dependency is not a special case |

MySQL has no native `rowversion`/`timestamp` column like SQL Server, so optimistic concurrency
is implemented by hand: `Job` and `JobExecution` both have an `int RowVersion` configured as an
EF concurrency token, incremented in `AppDbContext.SaveChanges` for every modified row. EF's
normal "does the original value still match what's in the database" check does the rest.

## Retries and failure handling

- Every `JobExecution` row is one **attempt**. A logical "run" that's retried twice produces
  three rows sharing the same `ExecutionGroupId` - this keeps the state machine trivial (every
  row only ever moves `Pending → Running → {Succeeded | Failed | Cancelled}`, never back) and
  makes the attempt history trivial to query and display as-is.
- Failed attempts back off exponentially with jitter: `baseDelay * 2^(attempt-1)`, +/-20%
  jitter, capped at 1 hour (`RetryPolicy.GetBackoffDelay`, unit tested). Jitter avoids many
  failing jobs retrying in lockstep and hammering a struggling downstream API all at once.
  `MaxAttempts` and the base delay are configurable per job.
- Once `MaxAttempts` is reached the execution is terminally `Failed`. A user can still hit
  **Retry** on it from the UI, which starts a *fresh* attempt chain (new `ExecutionGroupId`,
  back to attempt 1 of the job's configured max) - that's a deliberate choice: automatic retries
  for that run are exhausted, but "try again" is a new decision the user is explicitly making,
  so it gets a full new budget rather than continuing a chain that's already used up.
- `Pending` executions can be cancelled; once claimed (`Running`) it's too late, since the HTTP
  call may already be in flight with real side effects on the far end.

## Database decisions

- **Guid primary keys** everywhere (jobs, executions, users) rather than auto-increment ints -
  avoids leaking sequential IDs and makes `IdempotencyKey`/`ExecutionGroupId` generation trivial
  without a round trip.
- **`(JobId, ScheduledFor)` unique index** on `JobExecutions` is the main duplicate-scheduling
  guard. It works because every attempt of a retry chain gets a *different* `ScheduledFor` (the
  backoff time), so it never collides with legitimate retries - only with a genuine double
  schedule of the same tick.
- **`IdempotencyKey` unique index** covers "Run Now" double-clicks and any client-side retry of
  a flaky request; it's always populated (including for scheduled/retry rows, using a
  deterministic value) so there's one mechanism instead of two.
- **Indexes** on `(Status, ScheduledFor)` for the worker's claim query and on
  `(IsEnabled, ScheduleType, NextRunAt)` for the scheduler's query - both are hit every few
  seconds, so both are covered rather than relying on a full scan.
- **No soft-deletes / audit table.** Deleting a job cascades to its executions. For a real
  product I'd keep execution history even after a job is deleted (compliance/debugging value),
  but that's more schema than this assignment's scope justified.

## Product decisions

- Scoped the action type to "HTTP call" (see above) rather than trying to support several
  half-finished job types.
- Kept the UI to: job list with search/enabled filter, job detail with execution history +
  run/cancel/retry, a global execution list across jobs, and a create/edit form - the minimum
  bullet list from the brief plus filtering/search and a global execution view, deliberately
  skipping notifications, real-time (websocket) updates, and job statistics dashboards. The job
  detail and list pages poll every few seconds instead of pushing over a websocket, which is a
  much smaller amount of code for very similar perceived responsiveness at this scale.
- Manual retry always starts a fresh attempt chain (see "Retries" above) - I considered
  resuming the same chain instead, but that would silently exceed the job's configured
  `MaxAttempts`, which felt like the wrong default.

## Known limitations

- **Worker and API share a process.** A burst of slow external API calls could theoretically
  compete with HTTP request handling for thread-pool threads on the same instance. Acceptable
  for this scope; see "What I'd improve".
- **No real message broker** - the DB-as-queue approach is simple and correct at the row-locking
  level, but doesn't give you broker features like priority queues, dead-letter routing, or
  back-pressure signaling. It also means claim throughput is bounded by how fast Postgres...
  er, MySQL can do `FOR UPDATE SKIP LOCKED`, which is fine at the scale a job automation tool
  for one team would see, not at "thousands of jobs/second" scale.
- **The stale-execution reaper scans all `Running` rows** every 15s rather than using an
  indexed "due for reaping" query. Fine at hundreds/low-thousands of concurrently running
  executions; wouldn't scale to a very large fleet without adding a `LeaseExpiresAt` column
  and indexing on it directly.
- **No rate limiting or per-target-domain concurrency caps** - a job configured to call the same
  flaky endpoint on a 10-second interval will just keep trying it every 10 seconds forever
  (subject to its own retry backoff per attempt, but not throttled at the job level).
- **No email/webhook notification on failure** - you have to look at the UI to notice a job has
  started failing.
- **Single MySQL instance, no read replica** - fine for this scope; a heavier read load (e.g.
  the global execution list at real scale) would eventually want one.
- **The database migration is generated at Docker build time rather than committed** - see the
  README's "A note on how this repository was produced" for why, and how to regenerate it if
  needed.

## What I'd improve with more time

1. Split the worker out of the API process into its own deployable, so API latency and job
   throughput scale independently, and so a burst of running jobs can't affect request handling.
2. Add a `LeaseExpiresAt` column set at claim time so the reaper query can be a targeted index
   scan (`WHERE Status = Running AND LeaseExpiresAt < NOW()`) instead of scanning every running row.
3. WebSocket/SignalR push for execution status instead of polling, once there's enough usage to
   justify the extra moving part.
4. Per-job concurrency limits and a simple token-bucket rate limit per target host, so one badly
   configured job can't hammer a downstream dependency.
5. Notification hooks (email/Slack/webhook) on a job transitioning to permanently `Failed`.
6. Structured job "statistics" view (success rate over time, p50/p95 duration) - the data is
   already there in `JobExecutions`, it just isn't aggregated into a dashboard yet.
7. Soft-delete jobs and retain execution history independently, for audit/debugging after a job
   is removed.
