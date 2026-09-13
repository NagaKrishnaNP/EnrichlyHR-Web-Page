export type ScheduleType = 'Manual' | 'Interval';
export type ExecutionStatus = 'Pending' | 'Running' | 'Succeeded' | 'Failed' | 'Cancelled';
export type TriggerType = 'Scheduled' | 'Manual' | 'Retry';

export interface JobStats {
  totalExecutions: number;
  succeededCount: number;
  failedCount: number;
  lastRunAt: string | null;
  lastStatus: ExecutionStatus | null;
}

export interface Job {
  id: string;
  name: string;
  description: string | null;
  httpMethod: string;
  targetUrl: string;
  headers: Record<string, string> | null;
  body: string | null;
  timeoutSeconds: number;
  scheduleType: ScheduleType;
  intervalSeconds: number | null;
  isEnabled: boolean;
  nextRunAt: string | null;
  maxAttempts: number;
  retryBaseDelaySeconds: number;
  createdAt: string;
  updatedAt: string;
  rowVersion: number;
  stats: JobStats;
}

export interface JobFormValue {
  name: string;
  description: string | null;
  httpMethod: string;
  targetUrl: string;
  headersText: string; // raw textarea, "Key: Value" per line
  body: string | null;
  timeoutSeconds: number;
  scheduleType: ScheduleType;
  intervalSeconds: number | null;
  isEnabled: boolean;
  maxAttempts: number;
  retryBaseDelaySeconds: number;
}
