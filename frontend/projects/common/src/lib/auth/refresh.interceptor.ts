import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, finalize, Observable, share, switchMap, tap, throwError } from 'rxjs';
import { AuthApi, AuthResponse } from './auth.api';
import { ApiError } from '../api/api-error';
import { SessionStore } from './session.store';

const REFRESH_URL = '/api/Auth/refresh';

/**
 * Holds the single in-flight refresh call so concurrent 401s share it rather
 * than each starting their own (`FE-11`). An injectable singleton rather than
 * a module-level variable, so each test's DI container starts clean instead
 * of leaking state into the next test.
 */
@Injectable({ providedIn: 'root' })
export class RefreshCoordinator {
  private readonly api = inject(AuthApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router, { optional: true });

  private inFlight: Observable<AuthResponse> | null = null;

  /** Starts a refresh if one is not already running, and returns it either way. */
  refresh(): Observable<AuthResponse> {
    if (this.inFlight) {
      return this.inFlight;
    }

    const accessToken = this.session.token();
    const refreshToken = this.session.refreshToken();

    if (!accessToken || !refreshToken) {
      this.expireSession();
      return throwError(() => new Error('No session to refresh.'));
    }

    this.inFlight = this.api.refresh(accessToken, refreshToken).pipe(
      tap((response) => this.session.updateTokens(response)),
      catchError((error: unknown) => {
        // A refresh failure means the session is over. Cleared once, here —
        // never retried through this same path (the classic infinite loop
        // this design exists to prevent).
        this.expireSession();
        return throwError(() => error);
      }),
      finalize(() => {
        this.inFlight = null;
      }),
      share(),
    );

    return this.inFlight;
  }

  expireSession(): void {
    this.session.signOut();

    // A failed refresh must not leave the user on a protected screen with a
    // cleared session and a stream of failing requests.
    if (this.router && !this.router.url.startsWith('/login')) {
      void this.router.navigate(['/login'], {
        queryParams: { returnUrl: this.router.url },
      }).catch(() => undefined);
    }
  }
}

function isUnauthorized(error: unknown): boolean {
  return (error instanceof ApiError || error instanceof HttpErrorResponse) && error.status === 401;
}

function isPortalForbidden(error: unknown, url: string): boolean {
  return error instanceof ApiError
    && error.status === 403
    && error.code === 'FORBIDDEN_ACCESS'
    && url.includes('/api/portal/');
}

/**
 * On a 401 from any request other than the refresh call itself, attempts
 * exactly one refresh and retries the original request once with the new
 * token. Concurrent 401s share one refresh (`RefreshCoordinator`). A failed
 * refresh clears the session and lets the failure propagate — the next
 * guarded navigation sends the user to `/login`.
 *
 * Sits before `authInterceptor` in the chain so the retried request picks up
 * the freshly stored access token when it re-enters the chain.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.url === REFRESH_URL) {
    return next(req);
  }

  const coordinator = inject(RefreshCoordinator);

  return next(req).pipe(
    catchError((error: unknown) => {
      // Portal endpoints require a CustomerId claim. An older token, or a
      // staff account used in the portal, cannot satisfy that requirement;
      // return the user to login rather than leaving the page stuck on 403.
      if (isPortalForbidden(error, req.url)) {
        coordinator.expireSession();
        return throwError(() => error);
      }

      if (!isUnauthorized(error)) {
        return throwError(() => error);
      }

      return coordinator.refresh().pipe(switchMap(() => next(req)));
    }),
  );
};
