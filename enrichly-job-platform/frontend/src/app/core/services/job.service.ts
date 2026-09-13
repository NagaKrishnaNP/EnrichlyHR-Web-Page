import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Job } from '../models/job.model';
import { Execution } from '../models/execution.model';

export interface CreateOrUpdateJobPayload {
  name: string;
  description: string | null;
  httpMethod: string;
  targetUrl: string;
  headers: Record<string, string> | null;
  body: string | null;
  timeoutSeconds: number;
  scheduleType: 'Manual' | 'Interval';
  intervalSeconds: number | null;
  maxAttempts: number;
  retryBaseDelaySeconds: number;
}

@Injectable({ providedIn: 'root' })
export class JobService {
  private base = `${environment.apiBaseUrl}/jobs`;

  constructor(private http: HttpClient) {}

  list(search?: string, enabled?: boolean): Observable<Job[]> {
    const params: Record<string, string> = {};
    if (search) params['search'] = search;
    if (enabled !== undefined) params['enabled'] = String(enabled);
    return this.http.get<Job[]>(this.base, { params });
  }

  get(id: string): Observable<Job> {
    return this.http.get<Job>(`${this.base}/${id}`);
  }

  create(payload: CreateOrUpdateJobPayload): Observable<Job> {
    return this.http.post<Job>(this.base, payload);
  }

  update(id: string, payload: CreateOrUpdateJobPayload & { isEnabled: boolean; rowVersion: number }): Observable<Job> {
    return this.http.put<Job>(`${this.base}/${id}`, payload);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  run(id: string, idempotencyKey?: string): Observable<Execution> {
    return this.http.post<Execution>(`${this.base}/${id}/run`, { idempotencyKey: idempotencyKey ?? null });
  }
}
