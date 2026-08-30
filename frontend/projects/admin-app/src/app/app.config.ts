import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import {
  authInterceptor,
  envelopeInterceptor,
  REALTIME_CONFIG,
  REALTIME_HUB_PATH,
  refreshInterceptor,
} from 'common';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    // FEAT-15 — point the SignalR client at the in-app notification hub.
    { provide: REALTIME_CONFIG, useValue: { hubUrl: REALTIME_HUB_PATH } },
    provideBrowserGlobalErrorListeners(),
    // withComponentInputBinding lets a route parameter arrive as a component input()
    // signal, so TicketDetailComponent takes its id the same way any other input arrives
    // rather than reaching into ActivatedRoute.
    provideRouter(routes, withComponentInputBinding()),
    // Order matters both ways. For the outbound leg: auth attaches the
    // bearer token before envelope forwards the request to the backend.
    // For the inbound leg (interceptors see the response in reverse): the
    // envelope interceptor converts a failure into ApiError first, then
    // refresh reacts to a 401 by retrying via `next(req)` — which re-enters
    // the chain at auth, so the retried request picks up the freshly
    // stored token rather than the stale one it started with.
    provideHttpClient(
      withInterceptors([refreshInterceptor, authInterceptor, envelopeInterceptor]),
    ),
  ],
};
