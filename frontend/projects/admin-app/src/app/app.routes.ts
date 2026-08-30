import { Routes } from '@angular/router';
import { authGuard, roleGuard } from 'common';
import { AdminShell } from './layout/shell.component';

export const routes: Routes = [
  // Anonymous: the screen that obtains a session cannot require one.
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component'),
  },

  // Everything else renders inside the shell and requires a session. The guard
  // sits on the parent, so a new child route is protected by default rather
  // than by whoever adds it remembering to.
  {
    path: '',
    component: AdminShell,
    canActivate: [authGuard],
    children: [
      {
        // The post-sign-in landing route. First in the list because it is the default child
        // below, and reading the two together is how a reviewer checks they agree.
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component'),
      },
      {
        path: 'agent-workspace',
        loadComponent: () => import('./features/agent-workspace/agent-workspace.component'),
      },
      {
        path: 'tickets',
        loadComponent: () => import('./features/tickets/ticket-queue.component'),
      },
      {
        // Before the ':id' detail route FEAT-06 adds, or 'new' would match as an id.
        path: 'tickets/new',
        loadComponent: () => import('./features/tickets/ticket-create.component'),
      },
      {
        // AFTER 'tickets/new', or 'new' would match here as an id.
        path: 'tickets/:id',
        loadComponent: () => import('./features/tickets/ticket-detail.component'),
      },
      {
        path: 'customers',
        loadComponent: () => import('./features/customers/customer-list.component'),
      },
      {
        // Before 'customers/:id', or 'new' would match there as an id.
        path: 'customers/new',
        loadComponent: () => import('./features/customers/customer-create.component'),
      },
      {
        // AFTER 'customers/new', for the same reason.
        path: 'customers/:id',
        loadComponent: () => import('./features/customers/customer-detail.component'),
      },
      {
        path: 'users',
        // Hiding the nav item is a courtesy; this guard and the Admin
        // policy on the endpoints are the control (AUTH-22).
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/users/users.component'),
      },
      {
        path: 'departments',
        // Same reasoning as 'users': the guard here is a courtesy, the Admin policy on
        // /api/Departments' mutations is the control (FEAT-16, AC-120).
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/organisation/departments.component'),
      },
      {
        path: 'sla-policies',
        // Same reasoning again — the Admin policy on /api/SLAPolicies' mutations is the control.
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/organisation/sla-policies.component'),
      },
{
        // The reports hub — a supervisor-facing overview of the four report endpoints, in the
        // shape of the management_analytics_sla_performance mockup (management dashboard).
        path: 'reports/overview',
        canActivate: [roleGuard('Supervisor', 'Admin')],
        loadComponent: () => import('./features/reports/reports-overview.component'),
      },
      {
        path: 'reports/ticket-volume',
        // FEAT-19+ frontend addendum — matches the backend's Supervisor policy (Supervisor OR
        // Admin), AC-164.
        canActivate: [roleGuard('Supervisor', 'Admin')],
        loadComponent: () => import('./features/reports/ticket-volume-report.component'),
      },
      {
        path: 'reports/sla-performance',
        canActivate: [roleGuard('Supervisor', 'Admin')],
        loadComponent: () => import('./features/reports/sla-performance-report.component'),
      },
      {
        path: 'reports/agent-performance',
        canActivate: [roleGuard('Supervisor', 'Admin')],
        loadComponent: () => import('./features/reports/agent-performance-report.component'),
      },
      {
        path: 'reports/live-queue',
        canActivate: [roleGuard('Supervisor', 'Admin')],
        loadComponent: () => import('./features/reports/live-queue.component'),
      },
      {
        // US-605 (reopened by sprint storydept) — CSAT from the portal's post-resolution surveys.
        path: 'reports/csat',
        canActivate: [roleGuard('Supervisor', 'Admin')],
        loadComponent: () => import('./features/reports/csat-report.component'),
      },
      {
        path: 'audit-log',
        // The Admin policy on GET /api/admin/audit-log itself is the control (AC-143).
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/admin/audit-log.component'),
      },
      {
        path: 'settings',
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/admin/platform-settings.component'),
      },
      {
        path: 'permissions',
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/admin/permissions.component'),
      },
      {
        // FEAT-18 staff authoring surface (US-509..512). ContentManager is the platform's
        // content role; Admin can always. The InternalApi endpoints are the real control.
        path: 'kb-admin',
        canActivate: [roleGuard('Admin', 'ContentManager')],
        loadComponent: () => import('./features/kb/kb-admin.component'),
      },
      {
        path: 'kb-admin/:id',
        canActivate: [roleGuard('Admin', 'ContentManager')],
        loadComponent: () => import('./features/kb/kb-content-detail.component'),
      },
      {
        // Replaces the old standalone 'account/password' route — change-password now lives as a
        // section of the profile page, reached from the sidebar's identity footer.
        path: 'profile',
        loadComponent: () => import('./features/account/profile.component'),
      },
      {
        path: 'chat',
        canActivate: [roleGuard('Agent', 'Supervisor', 'Admin')],
        loadComponent: () => import('./features/chat/chat-queue.component'),
      },
      {
        path: 'chat/sessions/:id',
        canActivate: [roleGuard('Agent', 'Supervisor', 'Admin')],
        loadComponent: () => import('./features/chat/chat-session.component'),
      },
      {
        path: 'forbidden',
        loadComponent: () => import('./features/errors/forbidden.component'),
      },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    ],
  },

  { path: '**', redirectTo: '' },
];

