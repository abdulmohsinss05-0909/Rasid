import { Routes } from '@angular/router';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';
import { Roles } from './core/auth/auth.service';

/** All feature pages are lazy-loaded; guards mirror the API's authorisation policies. */
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in · Rasid',
    loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'overview' },
      {
        path: 'overview',
        title: 'Overview · Rasid',
        loadComponent: () => import('./features/overview/overview-page').then((m) => m.OverviewPage),
      },
      {
        path: 'topics',
        title: 'Topics · Rasid',
        loadComponent: () => import('./features/topics/topic-list-page').then((m) => m.TopicListPage),
      },
      {
        path: 'topics/new',
        title: 'New topic · Rasid',
        canActivate: [roleGuard(Roles.admin, Roles.analyst)],
        loadComponent: () => import('./features/topics/topic-form-page').then((m) => m.TopicFormPage),
      },
      {
        path: 'topics/:id/edit',
        title: 'Edit topic · Rasid',
        canActivate: [roleGuard(Roles.admin, Roles.analyst)],
        loadComponent: () => import('./features/topics/topic-form-page').then((m) => m.TopicFormPage),
      },
      {
        path: 'topics/:id',
        title: 'Dashboard · Rasid',
        // Chart.js is registered only for this lazily loaded route, keeping it out of the initial bundle.
        providers: [provideCharts(withDefaultRegisterables())],
        loadComponent: () => import('./features/dashboard/dashboard-page').then((m) => m.DashboardPage),
      },
      {
        path: 'system',
        title: 'System status · Rasid',
        canActivate: [roleGuard(Roles.admin)],
        loadComponent: () => import('./features/system/system-page').then((m) => m.SystemPage),
      },
      {
        path: 'about',
        title: 'Method & compliance · Rasid',
        loadComponent: () => import('./features/about/about-page').then((m) => m.AboutPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
