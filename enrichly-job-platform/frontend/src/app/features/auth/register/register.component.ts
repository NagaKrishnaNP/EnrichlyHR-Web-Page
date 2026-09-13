import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="auth-page">
      <form class="card auth-card" (ngSubmit)="submit()">
        <h1>Create account</h1>
        <p class="subtitle">Enrichly Job Automation Platform</p>

        @if (error()) {
          <div class="banner banner-error">{{ error() }}</div>
        }

        <div class="field">
          <label for="displayName">Name</label>
          <input id="displayName" class="input" name="displayName" [(ngModel)]="displayName" required />
        </div>
        <div class="field">
          <label for="email">Email</label>
          <input id="email" class="input" type="email" name="email" [(ngModel)]="email" required autocomplete="username" />
        </div>
        <div class="field">
          <label for="password">Password</label>
          <input id="password" class="input" type="password" name="password" [(ngModel)]="password" required minlength="8" autocomplete="new-password" />
          <span class="hint">At least 8 characters.</span>
        </div>

        <button class="btn btn-primary" type="submit" [disabled]="loading()" style="width: 100%">
          @if (loading()) { <span class="spinner"></span> } Create account
        </button>

        <p class="switch">Already have an account? <a routerLink="/login">Log in</a></p>
      </form>
    </div>
  `,
  styles: [`
    .auth-page { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; }
    .auth-card { width: 380px; padding: 32px; }
    .subtitle { color: var(--color-text-muted); margin-bottom: 20px; font-size: 13px; }
    .switch { margin-top: 16px; font-size: 13px; text-align: center; color: var(--color-text-muted); }
    .hint { display: block; margin-top: 4px; font-size: 12px; color: var(--color-text-muted); }
  `]
})
export class RegisterComponent {
  displayName = '';
  email = '';
  password = '';
  loading = signal(false);
  error = signal<string | null>(null);

  constructor(private auth: AuthService, private router: Router) {}

  submit(): void {
    if (!this.email || !this.password || !this.displayName) return;
    this.loading.set(true);
    this.error.set(null);

    this.auth.register({ email: this.email, password: this.password, displayName: this.displayName }).subscribe({
      next: () => this.router.navigate(['/jobs']),
      error: (err) => {
        this.loading.set(false);
        const errors = err?.error?.errors as string[] | undefined;
        this.error.set(errors?.join(' ') ?? err?.error?.message ?? 'Could not create account.');
      }
    });
  }
}
