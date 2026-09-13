import { ExecutionStatus, TriggerType } from './job.model';

export interface Execution {
  id: string;
  jobId: string;
  jobName: string;
  executionGroupId: string;
  status: ExecutionStatus;
  triggerType: TriggerType;
  attemptNumber: number;
  maxAttempts: number;
  scheduledFor: string;
  startedAt: string | null;
  completedAt: string | null;
  durationMs: number | null;
  responseStatusCode: number | null;
  output: string | null;
  errorMessage: string | null;
  workerId: string | null;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
