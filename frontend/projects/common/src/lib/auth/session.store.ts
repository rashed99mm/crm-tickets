import { computed, inject, Injectable, signal } from '@angular/core';
import { AuthResponse } from './auth.api';
import { StoredSession, TokenStorage } from './token-storage';

interface JwtClaims {
  readonly 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string | readonly string[];
  readonly exp?: number;
}

/** ASP.NET Core Identity's default claim type URI for a JWT's role claim. */
const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

function decodeClaims(token: string | null): JwtClaims | null {
  if (!token) {
    return null;
  }

  const segments = token.split('.');
  if (segments.length !== 3) {
    return null;
  }

  try {
    const base64 = segments[1].replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(atob(base64)) as JwtClaims;
  } catch {
    // A malformed token is not an exceptional condition — it is an
    // unauthenticated user. Throwing here would break app startup for
    // anyone holding a stale or corrupted token.
    return null;
  }
}

function toStoredSession(response: AuthResponse): StoredSession {
  return {
    userId: response.userId,
    accessToken: response.accessToken,
    refreshToken: response.refreshToken,
    accessTokenExpiresAt: response.accessTokenExpiresAt,
    refreshTokenExpiresAt: response.refreshTokenExpiresAt,
    firstName: response.firstName,
    lastName: response.lastName,
  };
}

/**
 * The session, derived from the stored record rather than decoded piecemeal
 * from the token — the backend's access token carries no `name` claim, so
 * `displayName` must come from what `AuthApi` returned, not from the JWT.
 *
 * `isAuthenticated` and `roles` are still `computed` over the token signal
 * (the claims that *are* on the token), so those two facts can never drift
 * out of sync with what the server actually signed.
 *
 * This reads the token's claims without verifying its signature, which is
 * correct: the client cannot verify anything meaningfully, and the server
 * re-validates every request. The claims here drive UI affordances only.
 * Hiding a button is not authorization (AC-61).
 */
@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly storage = inject(TokenStorage);
  private readonly _session = signal<StoredSession | null>(this.storage.read());

  readonly token = computed(() => this._session()?.accessToken ?? null);
  readonly refreshToken = computed(() => this._session()?.refreshToken ?? null);
  readonly accessTokenExpiresAt = computed(() => this._session()?.accessTokenExpiresAt ?? null);
  readonly userId = computed(() => this._session()?.userId ?? null);

  private readonly claims = computed(() => decodeClaims(this.token()));

  readonly isAuthenticated = computed(() => this.claims() !== null);

  readonly roles = computed<readonly string[]>(() => {
    const role = this.claims()?.[ROLE_CLAIM];

    if (!role) {
      return [];
    }

    // ASP.NET serialises a single role as a string and several as an array.
    // Without this branch a one-role user appears to have none, and every
    // role check silently fails.
    return Array.isArray(role) ? role : [role];
  });

  readonly displayName = computed(() => {
    const session = this._session();
    return session ? `${session.firstName} ${session.lastName}`.trim() : null;
  });

  signIn(response: AuthResponse): void {
    const session = toStoredSession(response);
    this.storage.write(session);
    this._session.set(session);
  }

  /** Replaces the stored session with a refreshed pair, keeping identity fields. */
  updateTokens(response: AuthResponse): void {
    this.signIn(response);
  }

  signOut(): void {
    this.storage.clear();
    this._session.set(null);
  }

  hasRole(role: string): boolean {
    // SuperAdmin is the platform-wide role and must inherit every UI role check.
    return this.roles().includes(role) || this.roles().includes('SuperAdmin');
  }
}
