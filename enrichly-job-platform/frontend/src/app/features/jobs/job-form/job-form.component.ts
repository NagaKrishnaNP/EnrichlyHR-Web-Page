import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { JobService } from '../../../core/services/job.service';
import { Job, ScheduleType } from '../../../core/models/job.model';

@Component({
  selector: 'app-job-form',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="page narrow">
      <div class="page-header">
        <h1>{{ isEdit() ? 'Edit job' : 'New job' }}</h1>
        <a class="btn btn-secondary" [routerLink]="isEdit() ? ['/jobs', jobId] : ['/jobs']">Cancel</a>
      </div>

      @if (error()) { <div class="banner banner-error">{{ error() }}</div> }
      @if (conflict()) {
        <div class="banner banner-error">
          This job was changed elsewhere since you opened it. <a (click)="reload()" href="javascript:void(0)">Reload the latest version</a> before saving again.
        </div>
      }

      @if (loading()) {
        <div class="empty-state"><span class="spinner"></span> Loading...</div>
      } @else {
        <form class="card form" (ngSubmit)="submit()">
          <h3>Basics</h3>
          <div class="field">
            <label for="name">Name</label>
            <input id="name" class="input" name="name" [(ngModel)]="model.name" required maxlength="200" />
          </div>
          <div class="field">
            <label for="description">Description (optional)</label>
            <textarea id="description" class="input" name="description" rows="2" [(ngModel)]="model.description"></textarea>
          </div>

          <h3>What it does</h3>
          <div class="row">
            <div class="field method-field">
              <label for="method">Method</label>
              <select id="method" class="input" name="method" [(ngModel)]="model.httpMethod">
                <option>GET</option><option>POST</option><option>PUT</option><option>PATCH</option><option>DELETE</option>
              </select>
            </div>
            <div class="field grow">
              <label for="url">Target URL</label>
              <input id="url" class="input" name="url" [(ngModel)]="model.targetUrl" required placeholder="https://api.example.com/endpoint" />
            </div>
          </div>
          <div class="field">
            <label for="headers">Headers (optional, one per line as "Key: Value")</label>
            <textarea id="headers" class="input mono" name="headers" rows="3" [(ngModel)]="model.headersText" placeholder="Authorization: Bearer ..."></textarea>
          </div>
          <div class="field">
            <label for="body">Request body (optional, sent for POST/PUT/PATCH)</label>
            <textarea id="body" class="input mono" name="body" rows="4" [(ngModel)]="model.body" placeholder="JSON body"></textarea>
          </div>
          <div class="field">
            <label for="timeout">Timeout (seconds)</label>
            <input id="timeout" class="input narrow-input" type="number" name="timeout" min="1" max="300" [(ngModel)]="model.timeoutSeconds" />
          </div>

          <h3>Schedule</h3>
          <div class="field">
            <label>
              <input type="radio" name="scheduleType" value="Manual" [(ngModel)]="model.scheduleType" /> Manual only (Run Now button)
            </label>
            <label>
              <input type="radio" name="scheduleType" value="Interval" [(ngModel)]="model.scheduleType" /> On a repeating interval
            </label>
          </div>
          @if (model.scheduleType === 'Interval') {
            <div class="field">
              <label for="interval">Run every (seconds, minimum 10)</label>
              <input id="interval" class="input narrow-input" type="number" name="interval" min="10" [(ngModel)]="model.intervalSeconds" />
            </div>
          }
          @if (isEdit()) {
            <div class="field">
              <label><input type="checkbox" name="isEnabled" [(ngModel)]="model.isEnabled" /> Enabled</label>
            </div>
          }

          <h3>Retries</h3>
          <div class="row">
            <div class="field">
              <label for="maxAttempts">Max attempts</label>
              <input id="maxAttempts" class="input narrow-input" type="number" name="maxAttempts" min="1" max="10" [(ngModel)]="model.maxAttempts" />
            </div>
            <div class="field">
              <label for="retryDelay">Retry base delay (seconds)</label>
              <input id="retryDelay" class="input narrow-input" type="number" name="retryDelay" min="1" [(ngModel)]="model.retryBaseDelaySeconds" />
            </div>
          </div>
          <p class="hint">Failed executions retry with exponential backoff starting at the base delay (e.g. 30s, 60s, 120s...) up to max attempts.</p>

          <div class="form-actions">
            <button class="btn btn-primary" type="submit" [disabled]="saving()">
              @if (saving()) { <span class="spinner"></span> } {{ isEdit() ? 'Save changes' : 'Create job' }}
            </button>
          </div>
        </form>
      }
    </div>
  `,
  styles: [`
    .narrow { max-width: 720px; }
    .form { padding: 24px; }
    h3 { margin-top: 24px; margin-bottom: 12px; font-size: 14px; color: var(--color-text-muted); text-transform: uppercase; letter-spacing: 0.04em; }
    h3:first-child { margin-top: 0; }
    .row { display: flex; gap: 12px; }
    .method-field { width: 120px; }
    .grow { flex: 1; }
    .narrow-input { max-width: 140px; }
    .mono { font-family: 'SFMono-Regular', Consolas, monospace; font-size: 13px; }
    .hint { font-size: 12.5px; color: var(--color-text-muted); margin-top: -8px; margin-bottom: 16px; }
    .form-actions { margin-top: 24px; padding-top: 16px; border-top: 1px solid var(--color-border); }
    label input[type="radio"], label input[type="checkbox"] { margin-right: 6px; }
  `]
})
export class JobFormComponent implements OnInit {
  jobId: string | null = null;
  loading = signal(false);
  saving = signal(false);
  error = signal<string | null>(null);
  conflict = signal(false);
  private rowVersion = 0;

  model = {
    name: '',
    description: '' as string | null,
    httpMethod: 'GET',
    targetUrl: '',
    headersText: '',
    body: '' as string | null,
    timeoutSeconds: 30,
    scheduleType: 'Manual' as ScheduleType,
    intervalSeconds: 60 as number | null,
    isEnabled: true,
    maxAttempts: 3,
    retryBaseDelaySeconds: 30
  };

  constructor(private jobService: JobService, private route: ActivatedRoute, private router: Router) {}

  isEdit(): boolean {
    return this.jobId !== null;
  }

  ngOnInit(): void {
    this.jobId = this.route.snapshot.paramMap.get('id');
    if (this.jobId) this.reload();
  }

  reload(): void {
    if (!this.jobId) return;
    this.loading.set(true);
    this.conflict.set(false);
    this.jobService.get(this.jobId).subscribe({
      next: (job) => {
        this.applyJob(job);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  private applyJob(job: Job): void {
    this.rowVersion = job.rowVersion;
    this.model = {
      name: job.name,
      description: job.description,
      httpMethod: job.httpMethod,
      targetUrl: job.targetUrl,
      headersText: job.headers ? Object.entries(job.headers).map(([k, v]) => `${k}: ${v}`).join('\n') : '',
      body: job.body,
      timeoutSeconds: job.timeoutSeconds,
      scheduleType: job.scheduleType,
      intervalSeconds: job.intervalSeconds ?? 60,
      isEnabled: job.isEnabled,
      maxAttempts: job.maxAttempts,
      retryBaseDelaySeconds: job.retryBaseDelaySeconds
    };
  }

  private parseHeaders(): Record<string, string> | null {
    const lines = this.model.headersText.split('\n').map((l) => l.trim()).filter(Boolean);
    if (lines.length === 0) return null;
    const headers: Record<string, string> = {};
    for (const line of lines) {
      const idx = line.indexOf(':');
      if (idx === -1) continue;
      headers[line.slice(0, idx).trim()] = line.slice(idx + 1).trim();
    }
    return Object.keys(headers).length ? headers : null;
  }

  submit(): void {
    if (!this.model.name || !this.model.targetUrl) return;
    this.error.set(null);
    this.conflict.set(false);
    this.saving.set(true);

    const payload = {
      name: this.model.name,
      description: this.model.description || null,
      httpMethod: this.model.httpMethod,
      targetUrl: this.model.targetUrl,
      headers: this.parseHeaders(),
      body: this.model.body || null,
      timeoutSeconds: Number(this.model.timeoutSeconds),
      scheduleType: this.model.scheduleType,
      intervalSeconds: this.model.scheduleType === 'Interval' ? Number(this.model.intervalSeconds) : null,
      maxAttempts: Number(this.model.maxAttempts),
      retryBaseDelaySeconds: Number(this.model.retryBaseDelaySeconds)
    };

    const done = (job: Job) => {
      this.saving.set(false);
      this.router.navigate(['/jobs', job.id]);
    };
    const fail = (err: any) => {
      this.saving.set(false);
      if (err?.status === 409) {
        this.conflict.set(true);
      } else {
        this.error.set(err?.error?.message ?? 'Could not save job.');
      }
    };

    if (this.isEdit() && this.jobId) {
      this.jobService.update(this.jobId, { ...payload, isEnabled: this.model.isEnabled, rowVersion: this.rowVersion }).subscribe({ next: done, error: fail });
    } else {
      this.jobService.create(payload).subscribe({ next: done, error: fail });
    }
  }
}
