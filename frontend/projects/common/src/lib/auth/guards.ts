import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from './session.store';

/**
 * AC-56: an unauthenticated visit to a protected route lands on login,
 * carrying the attempted url so the user returns there after signing in.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionStore);
  const router = inject(Router);

  if (session.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};

/**
 * Keeps a user out of a route their role cannot use.
 *
 * This does NOT replace server-side enforcement. Hiding a route is a
 * convenience; the endpoint still refuses the call (AC-4, AC-43).
 */
/**
 * Admits a caller holding ANY of the listed roles — `roleGuard('Admin')` still means exactly
 * what it meant before this change; `roleGuard('Supervisor', 'Admin')` is new, matching the
 * backend's `Supervisor` policy (`Supervisor` OR `Admin`) for the reports routes (AC-164).
 */
export function roleGuard(...roles: readonly string[]): CanActivateFn {
  return (_route, state) => {
    const session = inject(SessionStore);
    const router = inject(Router);

    if (!session.isAuthenticated()) {
      return router.createUrlTree(['/login'], {
        queryParams: { returnUrl: state.url },
      });
    }

    return roles.some((role) => session.hasRole(role)) ? true : router.createUrlTree(['/forbidden']);
  };
}
