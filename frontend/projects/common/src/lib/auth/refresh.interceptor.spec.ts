import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from '../api/envelope.interceptor';
import { refreshInterceptor } from './refresh.interceptor';
import { SessionStore } from './session.store';
import { AuthResponse } from './auth.api';

/** A JWT is three base64url segments; only the middle one is ever read. */
function fakeJwt(payload: Record<string, unknown>): string {
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');

  return `${encode({ alg: 'none' })}.${encode(payload)}.signature`;
}

const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

function authResponse(accessToken: string, refreshToken: string): AuthResponse {
  return {
    userId: 'u-1',
    email: 'dana@example.com',
    firstName: 'Dana',
    lastName: 'Support',
    accessToken,
    refreshToken,
    accessTokenExpiresAt: '2026-08-25T11:00:00+00:00',
    refreshTokenExpiresAt: '2026-09-08T10:00:00+00:00',
    roles: ['Admin'],
  };
}

function unauthorizedEnvelope() {
  return {
    success: false,
    code: 'INVALID_TOKEN',
    message: 'Invalid access token',
    data: null,
    errors: [],
  };
}

function forbiddenPortalEnvelope() {
  return {
    success: false,
    code: 'FORBIDDEN_ACCESS',
    message: 'Forbidden access',
    data: null,
    errors: [],
  };
}

describe('refreshInterceptor', () => {
  let http: HttpClient;
  let mock: HttpTestingController;
  let session: SessionStore;

  const oldAccessToken = fakeJwt({ [ROLE_CLAIM]: 'Admin' });
  const newAccessToken = fakeJwt({ [ROLE_CLAIM]: 'Admin' });

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([refreshInterceptor, envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    mock = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionStore);
    session.signIn(authResponse(oldAccessToken, 'old-refresh-token'));
  });

  afterEach(() => mock.verify());

  it('refreshes once and retries the original request on a single 401', async () => {
    const promise = new Promise((resolve, reject) =>
      http.get('/api/tickets').subscribe({ next: resolve, error: reject }));

    const first = mock.expectOne('/api/tickets');
    first.flush(unauthorizedEnvelope(), { status: 401, statusText: 'Unauthorized' });

    const refresh = mock.expectOne('/api/Auth/refresh');
    expect(refresh.request.body).toEqual({
      accessToken: oldAccessToken,
      refreshToken: 'old-refresh-token',
    });
    refresh.flush(authResponse(newAccessToken, 'new-refresh-token'));

    const retry = mock.expectOne('/api/tickets');
    retry.flush({ success: true, code: 'CON035', message: 'OK', data: { items: [] }, errors: [] });

    await expect(promise).resolves.toEqual({ items: [] });
    expect(session.token()).toBe(newAccessToken);
    expect(session.refreshToken()).toBe('new-refresh-token');
  });

  it('shares one refresh call across two concurrent 401s', async () => {
    const first$ = new Promise((resolve, reject) =>
      http.get('/api/tickets').subscribe({ next: resolve, error: reject }));
    const second$ = new Promise((resolve, reject) =>
      http.get('/api/customers').subscribe({ next: resolve, error: reject }));

    mock
      .expectOne('/api/tickets')
      .flush(unauthorizedEnvelope(), { status: 401, statusText: 'Unauthorized' });
    mock
      .expectOne('/api/customers')
      .flush(unauthorizedEnvelope(), { status: 401, statusText: 'Unauthorized' });

    // Exactly one refresh call, not two.
    const refreshRequests = mock.match('/api/Auth/refresh');
    expect(refreshRequests).toHaveLength(1);
    refreshRequests[0].flush(authResponse(newAccessToken, 'new-refresh-token'));

    mock.expectOne('/api/tickets').flush({ success: true, code: 'CON035', message: 'OK', data: { items: [] }, errors: [] });
    mock.expectOne('/api/customers').flush({ success: true, code: 'CON035', message: 'OK', data: { items: [] }, errors: [] });

    await expect(first$).resolves.toEqual({ items: [] });
    await expect(second$).resolves.toEqual({ items: [] });
  });

  it('clears the session and does not retry the refresh call itself when it fails', async () => {
    const caught = new Promise((resolve, reject) =>
      http.get('/api/tickets').subscribe({ next: reject, error: resolve }));

    mock
      .expectOne('/api/tickets')
      .flush(unauthorizedEnvelope(), { status: 401, statusText: 'Unauthorized' });

    mock
      .expectOne('/api/Auth/refresh')
      .flush(unauthorizedEnvelope(), { status: 401, statusText: 'Unauthorized' });

    await caught;

    expect(session.isAuthenticated()).toBe(false);
    // No second refresh attempt was made off the back of the failed one.
    mock.expectNone('/api/Auth/refresh');
  });

  it('clears a portal session when the token has no customer access', async () => {
    const caught = new Promise((resolve, reject) =>
      http.get('/api/portal/tickets').subscribe({ next: reject, error: resolve }));

    mock
      .expectOne('/api/portal/tickets')
      .flush(forbiddenPortalEnvelope(), { status: 403, statusText: 'Forbidden' });

    await caught;

    expect(session.isAuthenticated()).toBe(false);
    mock.expectNone('/api/Auth/refresh');
  });
});
