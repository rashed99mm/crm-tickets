import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { SessionStore } from './session.store';

/**
 * Attaches the bearer token when there is one.
 *
 * Registered BEFORE the envelope interceptor, so the token is on the request
 * before the envelope interceptor sees the response.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(SessionStore).token();

  if (!token) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
};
