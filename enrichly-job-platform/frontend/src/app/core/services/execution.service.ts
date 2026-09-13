import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Execution, PagedResult } from '../models/execution.model';
import { ExecutionStatus } from '../models/job.model';

@Injectable({ providedIn: 'root' })
export class ExecutionService {
  private base = `${environment.apiBaseUrl}/executions`;

  constructor(private http: HttpClient) {}

  list(opts: { jobId?: string; status?: ExecutionStatus; page?: number; pageSize?: number }): Observable<PagedResult<Execution>> {
    const params: Record<string, string> = {};
    if (opts.jobId) params['jobId'] = opts.jobId;
    if (opts.status) params['status'] = opts.status;
    params['page'] = String(opts.page ?? 1);
    params['pageSize'] = String(opts.pageSize ?? 25);
    return this.http.get<PagedResult<Execution>>(this.base, { params });
  }

  get(id: string): Observable<Execution> {
    return this.http.get<Execution>(`${this.base}/${id}`);
  }

  cancel(id: string): Observable<Execution> {
    return this.http.post<Execution>(`${this.base}/${id}/cancel`, {});
  }

  retry(id: string): Observable<Execution> {
    return this.http.post<Execution>(`${this.base}/${id}/retry`, {});
  }
}
