import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import {
  authInterceptor,
  envelopeInterceptor,
  REALTIME_HUB_PATH,
  refreshInterceptor,
  REALTIME_CONFIG,
} from 'common';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    // FEAT-15 — point the SignalR client at the in-app notification hub.
    { provide: REALTIME_CONFIG, useValue: { hubUrl: REALTIME_HUB_PATH } },
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // Refresh before auth so a 401 triggers a single-flight token refresh and
    // retries with the fresh token; auth attaches the token; envelope unwraps
    // the response. Same order as admin-app.
    provideHttpClient(
      withInterceptors([refreshInterceptor, authInterceptor, envelopeInterceptor]),
    ),
  ],
};
