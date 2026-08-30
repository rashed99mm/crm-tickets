import { TestBed } from '@angular/core/testing';
import { AuthResponse } from './auth.api';
import { SessionStore } from './session.store';
import { StoredSession, TokenStorage } from './token-storage';

const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

/** A JWT is three base64url segments; only the middle one is ever read. */
function fakeJwt(payload: Record<string, unknown>): string {
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');

  return `${encode({ alg: 'none' })}.${encode(payload)}.signature`;
}

function authResponse(overrides: Partial<AuthResponse> = {}): AuthResponse {
  return {
    userId: 'u-1',
    email: 'ada@example.com',
    firstName: 'Ada',
    lastName: 'Lovelace',
    accessToken: fakeJwt({ [ROLE_CLAIM]: ['Admin'] }),
    refreshToken: 'refresh-token-value',
    accessTokenExpiresAt: '2026-08-25T10:00:00+00:00',
    refreshTokenExpiresAt: '2026-09-08T10:00:00+00:00',
    roles: ['Admin'],
    ...overrides,
  };
}

describe('SessionStore', () => {
  let store: SessionStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [SessionStore, TokenStorage] });
    store = TestBed.inject(SessionStore);
  });

  it('starts unauthenticated with no roles', () => {
    expect(store.isAuthenticated()).toBe(false);
    expect(store.roles()).toEqual([]);
    expect(store.displayName()).toBeNull();
  });

  it('reads roles from the token and display name from the stored response on sign in', () => {
    store.signIn(authResponse({ accessToken: fakeJwt({ [ROLE_CLAIM]: ['Admin'] }) }));

    expect(store.isAuthenticated()).toBe(true);
    expect(store.roles()).toEqual(['Admin']);
    expect(store.displayName()).toBe('Ada Lovelace');
    expect(store.hasRole('Admin')).toBe(true);
    expect(store.hasRole('User')).toBe(false);
  });

  it('normalises a single-string role claim to an array', () => {
    // ASP.NET serialises one role as a string and several as an array.
    // Without this branch a single-role user appears to have no roles at all,
    // and every role check silently fails.
    store.signIn(authResponse({ accessToken: fakeJwt({ [ROLE_CLAIM]: 'User' }) }));

    expect(store.roles()).toEqual(['User']);
    expect(store.hasRole('User')).toBe(true);
  });

  it('treats a malformed token as unauthenticated rather than throwing', () => {
    store.signIn(authResponse({ accessToken: 'not-a-jwt' }));

    expect(store.isAuthenticated()).toBe(false);
    expect(store.roles()).toEqual([]);
  });

  it('carries the refresh token and access-token expiry', () => {
    store.signIn(
      authResponse({
        refreshToken: 'the-refresh-token',
        accessTokenExpiresAt: '2026-08-25T11:00:00+00:00',
      }),
    );

    expect(store.refreshToken()).toBe('the-refresh-token');
    expect(store.accessTokenExpiresAt()).toBe('2026-08-25T11:00:00+00:00');
  });

  it('clears everything on sign out', () => {
    store.signIn(authResponse());
    store.signOut();

    expect(store.isAuthenticated()).toBe(false);
    expect(store.token()).toBeNull();
    expect(store.refreshToken()).toBeNull();
    expect(localStorage.getItem('cs.session')).toBeNull();
  });

  it('restores a session from storage on construction', () => {
    const stored: StoredSession = {
      userId: 'u-2',
      accessToken: fakeJwt({ [ROLE_CLAIM]: 'User' }),
      refreshToken: 'stored-refresh-token',
      accessTokenExpiresAt: '2026-08-25T10:00:00+00:00',
      refreshTokenExpiresAt: '2026-09-08T10:00:00+00:00',
      firstName: 'Grace',
      lastName: 'Hopper',
    };
    localStorage.setItem('cs.session', JSON.stringify(stored));

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [SessionStore, TokenStorage] });
    const restored = TestBed.inject(SessionStore);

    expect(restored.isAuthenticated()).toBe(true);
    expect(restored.displayName()).toBe('Grace Hopper');
    expect(restored.refreshToken()).toBe('stored-refresh-token');
  });
});
