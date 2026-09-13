import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { JobService } from '../../../core/services/job.service';
import { ExecutionService } from '../../../core/services/execution.service';
import { Job } from '../../../core/models/job.model';
import { Execution } from '../../../core/models/execution.model';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-job-detail',
  standalone: true,
  imports: [RouterLink, DatePipe, StatusBadgeComponent],
  template: `
    @if (loading()) {
      <div class="page"><div class="empty-state"><span class="spinner"></span> Loading...</div></div>
    } @else if (job(); as j) {
      <div class="page">
        <div class="page-header">
          <div>
            <h1>{{ j.name }}</h1>
            @if (j.description) { <p class="muted">{{ j.description }}</p> }
          </div>
          <div class="header-actions">
            <a class="btn btn-secondary" [routerLink]="['/jobs', j.id, 'edit']">Edit</a>
            <button class="btn btn-primary" [disabled]="running()" (click)="run()">
              @if (running()) { <span class="spinner"></span> } Run now
            </button>
            <button class="btn btn-danger" (click)="remove()">Delete</button>
          </div>
        </div>

        @if (notice()) { <div class="banner banner-success">{{ notice() }}</div> }
        @if (error()) { <div class="banner banner-error">{{ error() }}</div> }

        <div class="grid">
          <div class="card summary">
            <div class="summary-row"><span class="k">Action</span><span class="v mono">{{ j.httpMethod }} {{ j.targetUrl }}</span></div>
            <div class="summary-row"><span class="k">Schedule</span><span class="v">{{ j.scheduleType === 'Interval' ? 'Every ' + formatInterval(j.intervalSeconds) : 'Manual only' }}</span></div>
            <div class="summary-row"><span class="k">Status</span><span class="v">{{ j.isEnabled ? 'Enabled' : 'Disabled' }}</span></div>
            <div class="summary-row"><span class="k">Next run</span><span class="v">{{ j.nextRunAt ? (j.nextRunAt | date: 'medium') : '—' }}</span></div>
            <div class="summary-row"><span class="k">Retry policy</span><span class="v">Up to {{ j.maxAttempts }} attempts, {{ j.retryBaseDelaySeconds }}s base backoff</span></div>
            <div class="summary-row"><span class="k">Total runs</span><span class="v">{{ j.stats.totalExecutions }} ({{ j.stats.succeededCount }} succeeded, {{ j.stats.failedCount }} failed)</span></div>
          </div>
        </div>

        <h2 class="section-title">Execution history</h2>

        @if (executionsLoading()) {
          <div class="empty-state"><span class="spinner"></span> Loading executions...</div>
        } @else if (executions().length === 0) {
          <div class="empty-state card">No executions yet. Click "Run now" to try it out.</div>
        } @else {
          <div class="card">
            <table>
              <thead>
                <tr>
                  <th>Status</th>
                  <th>Trigger</th>
                  <th>Attempt</th>
                  <th>Scheduled for</th>
                  <th>Duration</th>
                  <th>Result</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (e of executions(); track e.id) {
                  <tr>
                    <td><app-status-badge [status]="e.status"></app-status-badge></td>
                    <td class="small">{{ e.triggerType }}</td>
                    <td class="small">{{ e.attemptNumber }} / {{ e.maxAttempts }}</td>
                    <td class="small">{{ e.scheduledFor | date: 'medium' }}</td>
                    <td class="small">{{ e.durationMs !== null ? e.durationMs + ' ms' : '—' }}</td>
                    <td class="small result-cell">
                      @if (e.responseStatusCode) { <span class="mono">HTTP {{ e.responseStatusCode }}</span> }
                      @if (e.errorMessage) { <div class="err-msg">{{ e.errorMessage }}</div> }
                    </td>
                    <td class="actions">
                      @if (e.status === 'Pending') {
                        <button class="btn btn-secondary btn-sm" (click)="cancel(e)">Cancel</button>
                      }
                      @if (e.status === 'Failed') {
                        <button class="btn btn-secondary btn-sm" (click)="retry(e)">Retry</button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <div class="pagination">
            <button class="btn btn-secondary btn-sm" [disabled]="page() <= 1" (click)="changePage(page() - 1)">Previous</button>
            <span class="page-label">Page {{ page() }} of {{ totalPages() }}</span>
            <button class="btn btn-secondary btn-sm" [disabled]="page() >= totalPages()" (click)="changePage(page() + 1)">Next</button>
          </div>
        }
      </div>
    }
  `,
  styles: [`
    .muted { color: var(--color-text-muted); }
    .small { font-size: 12.5px; }
    .mono { font-family: 'SFMono-Regular', Consolas, monospace; }
    .header-actions { display: flex; gap: 8px; }
    .grid { margin-bottom: 28px; }
    .summary { padding: 18px 20px; }
    .summary-row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid var(--color-border); font-size: 13.5px; }
    .summary-row:last-child { border-bottom: none; }
    .k { color: var(--color-text-muted); }
    .v { font-weight: 500; text-align: right; max-width: 60%; word-break: break-all; }
    .section-title { margin-bottom: 12px; }
    .result-cell { max-width: 280px; }
    .err-msg { color: var(--color-danger); margin-top: 2px; }
    .actions { text-align: right; white-space: nowrap; }
    .pagination { display: flex; align-items: center; justify-content: center; gap: 14px; margin-top: 16px; }
    .page-label { font-size: 13px; color: var(--color-text-muted); }
  `]
})
export class JobDetailComponent implements OnInit, OnDestroy {
  jobId!: string;
  job = signal<Job | null>(null);
  loading = signal(true);
  running = signal(false);
  notice = signal<string | null>(null);
  error = signal<string | null>(null);

  executions = signal<Execution[]>([]);
  executionsLoading = signal(true);
  page = signal(1);
  totalPages = signal(1);
  private pageSize = 15;
  private pollHandle?: ReturnType<typeof setInterval>;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private jobService: JobService,
    private executionService: ExecutionService
  ) {}

  ngOnInit(): void {
    this.jobId = this.route.snapshot.paramMap.get('id')!;
    this.loadJob();
    this.loadExecutions();
    // Lightweight polling so status changes (Pending -> Running -> Succeeded/Failed) show up
    // without the user needing to refresh manually.
    this.pollHandle = setInterval(() => {
      this.loadJob(true);
      this.loadExecutions(true);
    }, 4000);
  }

  ngOnDestroy(): void {
    if (this.pollHandle) clearInterval(this.pollHandle);
  }

  loadJob(silent = false): void {
    if (!silent) this.loading.set(true);
    this.jobService.get(this.jobId).subscribe({
      next: (job) => {
        this.job.set(job);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  loadExecutions(silent = false): void {
    if (!silent) this.executionsLoading.set(true);
    this.executionService.list({ jobId: this.jobId, page: this.page(), pageSize: this.pageSize }).subscribe({
      next: (result) => {
        this.executions.set(result.items);
        this.totalPages.set(Math.max(1, Math.ceil(result.totalCount / result.pageSize)));
        this.executionsLoading.set(false);
      },
      error: () => this.executionsLoading.set(false)
    });
  }

  changePage(page: number): void {
    this.page.set(page);
    this.loadExecutions();
  }

  run(): void {
    this.running.set(true);
    this.notice.set(null);
    this.jobService.run(this.jobId).subscribe({
      next: () => {
        this.running.set(false);
        this.notice.set('Execution queued.');
        this.loadJob(true);
        this.loadExecutions(true);
      },
      error: (err) => {
        this.running.set(false);
        this.error.set(err?.error?.message ?? 'Could not run job.');
      }
    });
  }

  cancel(e: Execution): void {
    this.executionService.cancel(e.id).subscribe({
      next: () => this.loadExecutions(true),
      error: (err) => this.error.set(err?.error?.message ?? 'Could not cancel execution.')
    });
  }

  retry(e: Execution): void {
    this.executionService.retry(e.id).subscribe({
      next: () => this.loadExecutions(true),
      error: (err) => this.error.set(err?.error?.message ?? 'Could not retry execution.')
    });
  }

  remove(): void {
    if (!confirm('Delete this job and all of its execution history? This cannot be undone.')) return;
    this.jobService.delete(this.jobId).subscribe({
      next: () => this.router.navigate(['/jobs']),
      error: (err) => this.error.set(err?.error?.message ?? 'Could not delete job.')
    });
  }

  formatInterval(seconds: number | null): string {
    if (!seconds) return '';
    if (seconds % 3600 === 0) return `${seconds / 3600}h`;
    if (seconds % 60 === 0) return `${seconds / 60}m`;
    return `${seconds}s`;
  }
}
