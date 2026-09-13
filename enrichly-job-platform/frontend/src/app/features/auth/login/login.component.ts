import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="auth-page">
      <form class="card auth-card" (ngSubmit)="submit()">
        <h1>Log in</h1>
        <p class="subtitle">Enrichly Job Automation Platform</p>

        @if (error()) {
          <div class="banner banner-error">{{ error() }}</div>
        }

        <div class="field">
          <label for="email">Email</label>
          <input id="email" class="input" type="email" name="email" [(ngModel)]="email" required autocomplete="username" />
        </div>
        <div class="field">
          <label for="password">Password</label>
          <input id="password" class="input" type="password" name="password" [(ngModel)]="password" required autocomplete="current-password" />
        </div>

        <button class="btn btn-primary" type="submit" [disabled]="loading()" style="width: 100%">
          @if (loading()) { <span class="spinner"></span> } Log in
        </button>

        <p class="switch">No account yet? <a routerLink="/register">Create one</a></p>
      </form>
    </div>
  `,
  styles: [`
    .auth-page { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; }
    .auth-card { width: 380px; padding: 32px; }
    .subtitle { color: var(--color-text-muted); margin-bottom: 20px; font-size: 13px; }
    .switch { margin-top: 16px; font-size: 13px; text-align: center; color: var(--color-text-muted); }
  `]
})
export class LoginComponent {
  email = '';
  password = '';
  loading = signal(false);
  error = signal<string | null>(null);

  constructor(private auth: AuthService, private router: Router) {}

  submit(): void {
    if (!this.email || !this.password) return;
    this.loading.set(true);
    this.error.set(null);

    this.auth.login({ email: this.email, password: this.password }).subscribe({
      next: () => this.router.navigate(['/jobs']),
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message ?? 'Could not log in. Check your credentials and try again.');
      }
    });
  }
}
