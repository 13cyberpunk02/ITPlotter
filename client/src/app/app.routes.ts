import { Routes } from '@angular/router';
import { authGuard, guestGuard, adminGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'auth',
    canActivate: [guestGuard],
    children: [
      { path: 'login', loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent) },
      { path: 'register', loadComponent: () => import('./features/auth/register/register.component').then(m => m.RegisterComponent) },
      { path: '', redirectTo: 'login', pathMatch: 'full' },
    ],
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/layout/layout.component').then(m => m.LayoutComponent),
    children: [
      { path: '', loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent) },
      { path: 'stats', loadComponent: () => import('./features/stats/stats.component').then(m => m.StatsComponent) },
      {
        path: 'admin',
        canActivate: [adminGuard],
        children: [
          { path: 'printers', loadComponent: () => import('./features/printers/printers.component').then(m => m.PrintersComponent) },
          { path: 'print-jobs', loadComponent: () => import('./features/print-jobs/print-jobs.component').then(m => m.PrintJobsComponent) },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
