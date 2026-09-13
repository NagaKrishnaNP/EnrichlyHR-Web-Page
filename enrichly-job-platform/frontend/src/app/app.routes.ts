import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'jobs' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register.component').then((m) => m.RegisterComponent)
  },
  {
    path: 'jobs',
    canActivate: [authGuard],
    loadComponent: () => import('./features/jobs/job-list/job-list.component').then((m) => m.JobListComponent)
  },
  {
    path: 'jobs/new',
    canActivate: [authGuard],
    loadComponent: () => import('./features/jobs/job-form/job-form.component').then((m) => m.JobFormComponent)
  },
  {
    path: 'jobs/:id/edit',
    canActivate: [authGuard],
    loadComponent: () => import('./features/jobs/job-form/job-form.component').then((m) => m.JobFormComponent)
  },
  {
    path: 'jobs/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./features/jobs/job-detail/job-detail.component').then((m) => m.JobDetailComponent)
  },
  {
    path: 'executions',
    canActivate: [authGuard],
    loadComponent: () => import('./features/executions/execution-list.component').then((m) => m.ExecutionListComponent)
  },
  { path: '**', redirectTo: 'jobs' }
];
