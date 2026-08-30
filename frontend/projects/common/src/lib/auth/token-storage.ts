import { Injectable } from '@angular/core';

const KEY = 'cs.session';

/**
 * The persisted half of a session. Carries the refresh token and both
 * expiries (`FE-10`) alongside the access token and the identity fields the
 * real access token's claims do not carry (the backend's JWT has no `name`
 * claim — see `SessionStore`).
 */
export interface StoredSession {
  readonly userId: string;
  readonly accessToken: string;
  readonly refreshToken: string;
  readonly accessTokenExpiresAt: string;
  readonly refreshTokenExpiresAt: string;
  readonly firstName: string;
  readonly lastName: string;
}

/**
 * Isolates session persistence, so `SessionStore` stays testable and the
 * storage mechanism can change without touching it.
 *
 * `localStorage` is used because a page refresh must not sign the user out.
 * The trade-off against an httpOnly cookie is accepted for this slice and
 * recorded in the spec: a token in localStorage is reachable by any script
 * that gets injected, so the XSS surface matters more here than it would
 * with a cookie the page cannot read.
 *
 * Every access is guarded. Private browsing modes and some embedded webviews
 * throw on `localStorage` rather than returning null.
 */
@Injectable({ providedIn: 'root' })
export class TokenStorage {
  read(): StoredSession | null {
    try {
      const raw = localStorage.getItem(KEY);
      return raw ? (JSON.parse(raw) as StoredSession) : null;
    } catch {
      return null;
    }
  }

  write(session: StoredSession): void {
    try {
      localStorage.setItem(KEY, JSON.stringify(session));
    } catch {
      // Non-fatal: the session simply will not survive a refresh.
    }
  }

  clear(): void {
    try {
      localStorage.removeItem(KEY);
    } catch {
      // Nothing useful to do — the in-memory signal is already cleared.
    }
  }
}
