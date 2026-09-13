import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ExecutionService } from '../../core/services/execution.service';
import { JobService } from '../../core/services/job.service';
import { Execution } from '../../core/models/execution.model';
import { ExecutionStatus, Job } from '../../core/models/job.model';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

const STATUS_OPTIONS: ExecutionStatus[] = ['Pending', 'Running', 'Succeeded', 'Failed', 'Cancelled'];

@Component({
  selector: 'app-execution-list',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe, StatusBadgeComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>All executions</h1>
          <p class="muted">Every execution across every job, most recent first.</p>
        </div>
      </div>

      <div class="toolbar card">
        <select class="input filter-select" [(ngModel)]="jobIdFilter" (ngModelChange)="onFilterChange()">
          <option [ngValue]="undefined">All jobs</option>
          @for (job of jobs(); track job.id) {
            <option [ngValue]="job.id">{{ job.name }}</option>
          }
        </select>
        <select class="input filter-select" [(ngModel)]="statusFilter" (ngModelChange)="onFilterChange()">
          <option [ngValue]="undefined">All statuses</option>
          @for (status of statusOptions; track status) {
            <option [ngValue]="status">{{ status }}</option>
          }
        </select>
      </div>

      @if (loading()) {
        <div class="empty-state"><span class="spinner"></span> Loading executions...</div>
      } @else if (executions().length === 0) {
        <div class="empty-state card">No executions match these filters.</div>
      } @else {
        <div class="card">
          <table>
            <thead>
              <tr>
                <th>Job</th>
                <th>Status</th>
                <th>Trigger</th>
                <th>Attempt</th>
                <th>Scheduled for</th>
                <th>Duration</th>
                <th>Result</th>
              </tr>
            </thead>
            <tbody>
              @for (e of executions(); track e.id) {
                <tr>
                  <td><a [routerLink]="['/jobs', e.jobId]">{{ e.jobName }}</a></td>
                  <td><app-status-badge [status]="e.status"></app-status-badge></td>
                  <td class="small">{{ e.triggerType }}</td>
                  <td class="small">{{ e.attemptNumber }} / {{ e.maxAttempts }}</td>
                  <td class="small">{{ e.scheduledFor | date: 'medium' }}</td>
                  <td class="small">{{ e.durationMs !== null ? e.durationMs + ' ms' : '—' }}</td>
                  <td class="small result-cell">
                    @if (e.responseStatusCode) { <span class="mono">HTTP {{ e.responseStatusCode }}</span> }
                    @if (e.errorMessage) { <div class="err-msg">{{ e.errorMessage }}</div> }
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
  `,
  styles: [`
    .muted { color: var(--color-text-muted); }
    .small { font-size: 12.5px; }
    .mono { font-family: 'SFMono-Regular', Consolas, monospace; }
    .toolbar { display: flex; gap: 10px; padding: 12px; margin-bottom: 16px; }
    .filter-select { max-width: 220px; }
    .result-cell { max-width: 320px; }
    .err-msg { color: var(--color-danger); margin-top: 2px; }
    .pagination { display: flex; align-items: center; justify-content: center; gap: 14px; margin-top: 16px; }
    .page-label { font-size: 13px; color: var(--color-text-muted); }
  `]
})
export class ExecutionListComponent implements OnInit {
  executions = signal<Execution[]>([]);
  jobs = signal<Job[]>([]);
  loading = signal(true);
  page = signal(1);
  totalPages = signal(1);
  statusOptions = STATUS_OPTIONS;

  jobIdFilter: string | undefined = undefined;
  statusFilter: ExecutionStatus | undefined = undefined;
  private pageSize = 25;

  constructor(private executionService: ExecutionService, private jobService: JobService) {}

  ngOnInit(): void {
    this.jobService.list().subscribe({ next: (jobs) => this.jobs.set(jobs) });
    this.load();
  }

  onFilterChange(): void {
    this.page.set(1);
    this.load();
  }

  changePage(page: number): void {
    this.page.set(page);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.executionService
      .list({ jobId: this.jobIdFilter, status: this.statusFilter, page: this.page(), pageSize: this.pageSize })
      .subscribe({
        next: (result) => {
          this.executions.set(result.items);
          this.totalPages.set(Math.max(1, Math.ceil(result.totalCount / result.pageSize)));
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      });
  }
}
