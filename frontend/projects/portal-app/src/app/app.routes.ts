import { Routes } from '@angular/router';
import { authGuard } from 'common';
import { PortalShell } from './layout/shell.component';
import { PortalPublicShell } from './layout/shell-public.component';
import PortalLoginComponent from './features/auth/login.component';
import PortalSignupComponent from './features/auth/signup.component';
import PortalHomeComponent from './features/home/home.component';
import PortalSolutionComponent from './features/solution/solution.component';
import PortalDashboardComponent from './features/dashboard/dashboard.component';
import PortalSubmitComponent from './features/tickets/submit.component';
import PortalTicketListComponent from './features/tickets/list.component';
import PortalTicketDetailComponent from './features/tickets/detail.component';
import PortalKbListComponent from './features/kb/kb-list.component';
import PortalKbDetailComponent from './features/kb/kb-detail.component';
import PortalContentPageComponent from './features/kb/content-page.component';
import PortalFeedbackComponent from './features/feedback/feedback.component';
import PortalProfileComponent from './features/account/profile.component';

export const routes: Routes = [
  // Public area, rendered inside PortalPublicShell so the navbar + footer persist
  // across every public route (landing, auth, kb, contact, live-chat).
  {
    path: '',
    component: PortalPublicShell,
    children: [
      { path: '', component: PortalHomeComponent },
      { path: 'solution', component: PortalSolutionComponent },
      { path: 'signup', component: PortalSignupComponent },
      { path: 'login', component: PortalLoginComponent },
      {
        path: 'live-chat',
        loadComponent: () => import('./features/live-chat/live-chat-widget.component'),
      },
      {
        path: 'contact',
        loadComponent: () => import('./features/web-form/web-form.component'),
      },
      { path: 'kb', component: PortalKbListComponent },
      { path: 'kb/:id', component: PortalKbDetailComponent },
    ],
  },
  {
    // The authenticated area, rendered inside PortalShell with sidebar and topbar.
    path: 'app',
    component: PortalShell,
    canActivate: [authGuard],
    children: [
      { path: '', component: PortalDashboardComponent },
      { path: 'profile', component: PortalProfileComponent },
      { path: 'tickets/new', component: PortalSubmitComponent },
      { path: 'tickets', component: PortalTicketListComponent },
      { path: 'tickets/:id', component: PortalTicketDetailComponent },
      { path: 'kb', component: PortalKbListComponent },
      { path: 'kb/:id', component: PortalKbDetailComponent },
      { path: 'faq', component: PortalKbListComponent },
      { path: 'articles', component: PortalContentPageComponent },
      // The FAQ nav opens the full Stitch help-center surface, not the generic list.
      { path: 'faq', component: PortalKbListComponent },
      { path: 'articles', component: PortalContentPageComponent },
      { path: 'solution', component: PortalSolutionComponent },
      { path: 'feedback', component: PortalFeedbackComponent },
    ],
  },
  { path: '**', redirectTo: '' },
];

