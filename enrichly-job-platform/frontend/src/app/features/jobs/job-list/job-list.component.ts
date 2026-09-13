import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Job } from '../../../core/models/job.model';
import { JobService } from '../../../core/services/job.service';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-job-list',
  standalone: true,
  imports: [FormsModule, RouterLink, StatusBadgeComponent, DatePipe],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Jobs</h1>
          <p class="muted">Automated jobs you've created - HTTP calls that run on a schedule or on demand.</p>
        </div>
        <a class="btn btn-primary" routerLink="/jobs/new">+ New job</a>
      </div>

      <div class="toolbar card">
        <input class="input" placeholder="Search by name or description..." [(ngModel)]="search" (ngModelChange)="onFilterChange()" />
        <select class="input filter-select" [(ngModel)]="enabledFilter" (ngModelChange)="onFilterChange()">
          <option [ngValue]="undefined">All jobs</option>
          <option [ngValue]="true">Enabled only</option>
          <option [ngValue]="false">Disabled only</option>
        </select>
      </div>

      @if (loading()) {
        <div class="empty-state"><span class="spinner"></span> Loading jobs...</div>
      } @else if (jobs().length === 0) {
        <div class="empty-state card">
          <p><strong>No jobs yet.</strong></p>
          <p>Create your first job to call an API, trigger a webhook, or run something on a schedule.</p>
          <a class="btn btn-primary" routerLink="/jobs/new">+ New job</a>
        </div>
      } @else {
        <div class="card">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Action</th>
                <th>Schedule</th>
                <th>Last run</th>
                <th>Success / Fail</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (job of jobs(); track job.id) {
                <tr>
                  <td>
                    <a [routerLink]="['/jobs', job.id]" class="job-name">{{ job.name }}</a>
                    @if (job.description) { <div class="muted small">{{ job.description }}</div> }
                  </td>
                  <td class="mono small">{{ job.httpMethod }} {{ job.targetUrl }}</td>
                  <td>
                    @if (job.scheduleType === 'Interval') {
                      every {{ formatInterval(job.intervalSeconds) }}
                    } @else {
                      manual only
                    }
                  </td>
                  <td class="small">{{ job.stats.lastRunAt ? (job.stats.lastRunAt | date: 'short') : '—' }}</td>
                  <td class="small">{{ job.stats.succeededCount }} / {{ job.stats.failedCount }}</td>
                  <td>
                    @if (job.isEnabled) {
                      <span class="pill pill-on">Enabled</span>
                    } @else {
                      <span class="pill pill-off">Disabled</span>
                    }
                  </td>
                  <td class="actions">
                    <button class="btn btn-secondary btn-sm" [disabled]="runningId() === job.id" (click)="run(job)">
                      @if (runningId() === job.id) { <span class="spinner"></span> } Run now
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .muted { color: var(--color-text-muted); }
    .small { font-size: 12.5px; }
    .mono { font-family: 'SFMono-Regular', Consolas, monospace; }
    .toolbar { display: flex; gap: 10px; padding: 12px; margin-bottom: 16px; }
    .filter-select { max-width: 180px; }
    .job-name { font-weight: 600; color: var(--color-text); }
    .job-name:hover { color: var(--color-primary); }
    .pill { padding: 3px 9px; border-radius: 999px; font-size: 12px; font-weight: 600; }
    .pill-on { background: var(--color-success-bg); color: var(--color-success); }
    .pill-off { background: #f3f4f6; color: #9ca3af; }
    .actions { text-align: right; white-space: nowrap; }
  `]
})
export class JobListComponent implements OnInit {
  jobs = signal<Job[]>([]);
  loading = signal(true);
  runningId = signal<string | null>(null);
  search = '';
  enabledFilter: boolean | undefined = undefined;
  private filterTimeout?: ReturnType<typeof setTimeout>;

  constructor(private jobService: JobService) {}

  ngOnInit(): void {
    this.load();
  }

  onFilterChange(): void {
    clearTimeout(this.filterTimeout);
    this.filterTimeout = setTimeout(() => this.load(), 250);
  }

  load(): void {
    this.loading.set(true);
    this.jobService.list(this.search || undefined, this.enabledFilter).subscribe({
      next: (jobs) => {
        this.jobs.set(jobs);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  run(job: Job): void {
    this.runningId.set(job.id);
    this.jobService.run(job.id).subscribe({
      next: () => {
        this.runningId.set(null);
        this.load();
      },
      error: () => this.runningId.set(null)
    });
  }

  formatInterval(seconds: number | null): string {
    if (!seconds) return '';
    if (seconds % 3600 === 0) return `${seconds / 3600}h`;
    if (seconds % 60 === 0) return `${seconds / 60}m`;
    return `${seconds}s`;
  }
}
