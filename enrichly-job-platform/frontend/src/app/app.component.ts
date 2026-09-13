import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/services/auth.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="shell">
      @if (auth.isLoggedIn()) {
        <header class="topbar">
          <div class="topbar-inner">
            <a class="brand" routerLink="/jobs">Enrichly <span>Jobs</span></a>
            <nav class="nav">
              <a routerLink="/jobs" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: false }">Jobs</a>
              <a routerLink="/executions" routerLinkActive="active">All Executions</a>
            </nav>
            <div class="user">
              <span class="user-name">{{ auth.currentUser()?.displayName }}</span>
              <button class="btn btn-secondary btn-sm" (click)="logout()">Log out</button>
            </div>
          </div>
        </header>
      }
      <router-outlet></router-outlet>
    </div>
  `,
  styles: [`
    .shell { min-height: 100vh; }
    .topbar {
      background: #14161f;
      color: #fff;
      position: sticky;
      top: 0;
      z-index: 20;
    }
    .topbar-inner {
      max-width: 1100px;
      margin: 0 auto;
      padding: 0 24px;
      display: flex;
      align-items: center;
      gap: 28px;
      height: 56px;
    }
    .brand { color: #fff; font-weight: 700; font-size: 15px; }
    .brand span { color: #a5a6f3; }
    .nav { display: flex; gap: 18px; flex: 1; }
    .nav a { color: #c7c9d9; font-size: 13.5px; font-weight: 500; padding: 6px 0; border-bottom: 2px solid transparent; }
    .nav a:hover { color: #fff; text-decoration: none; }
    .nav a.active { color: #fff; border-bottom-color: #6366f1; }
    .user { display: flex; align-items: center; gap: 10px; }
    .user-name { font-size: 13px; color: #c7c9d9; }
  `]
})
export class AppComponent {
  constructor(public auth: AuthService, private router: Router) {}

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
