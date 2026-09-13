import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, RegisterRequest } from '../models/auth.model';

const STORAGE_KEY = 'enrichly.auth';

interface StoredAuth {
  token: string;
  email: string;
  displayName: string;
  userId: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  /** Reactive current-user signal so the nav bar / guards can react without manual polling. */
  readonly currentUser = signal<StoredAuth | null>(this.readFromStorage());

  constructor(private http: HttpClient) {}

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/auth/register`, request)
      .pipe(tap((res) => this.persist(res)));
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/auth/login`, request)
      .pipe(tap((res) => this.persist(res)));
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.currentUser.set(null);
  }

  getToken(): string | null {
    return this.currentUser()?.token ?? null;
  }

  isLoggedIn(): boolean {
    return this.currentUser() !== null;
  }

  private persist(res: AuthResponse): void {
    const stored: StoredAuth = {
      token: res.token,
      email: res.email,
      displayName: res.displayName,
      userId: res.userId
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
    this.currentUser.set(stored);
  }

  private readFromStorage(): StoredAuth | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as StoredAuth;
    } catch {
      return null;
    }
  }
}
