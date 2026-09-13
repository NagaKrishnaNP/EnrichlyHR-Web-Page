import { Component, Input } from '@angular/core';
import { ExecutionStatus } from '../../../core/models/job.model';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `<span class="badge" [class]="cssClass">{{ status }}</span>`,
  styles: [`
    .badge {
      display: inline-flex;
      align-items: center;
      padding: 3px 9px;
      border-radius: 999px;
      font-size: 12px;
      font-weight: 600;
      letter-spacing: 0.01em;
    }
    .pending { background: #eef0f3; color: #4b5563; }
    .running { background: #e0e7ff; color: #4338ca; }
    .succeeded { background: var(--color-success-bg); color: var(--color-success); }
    .failed { background: var(--color-danger-bg); color: var(--color-danger); }
    .cancelled { background: #f3f4f6; color: #9ca3af; }
  `]
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: ExecutionStatus;

  get cssClass(): string {
    return this.status.toLowerCase();
  }
}
