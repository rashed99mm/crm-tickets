import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthApi, AuthResponse } from './auth.api';

describe('AuthApi', () => {
  let api: AuthApi;
  let http: HttpTestingController;

  const payload: AuthResponse = {
    userId: 'u-1',
    email: 'dana@example.com',
    firstName: 'Dana',
    lastName: 'Support',
    accessToken: 'a.b.c',
    refreshToken: 'refresh-token-value',
    accessTokenExpiresAt: '2026-08-25T10:00:00+00:00',
    refreshTokenExpiresAt: '2026-09-08T10:00:00+00:00',
    roles: ['Admin'],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(AuthApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the credentials to the real login endpoint', () => {
    api.signIn('dana@example.com', 'a-password').subscribe();

    const request = http.expectOne('/api/Auth/login');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'dana@example.com',
      password: 'a-password',
    });

    request.flush(null);
  });

  it('returns the unwrapped AuthResponse', () => {
    // The envelope interceptor is not in this test chain, so the body here is
    // what the interceptor would already have unwrapped to `data`.
    let received: AuthResponse | undefined;
    api.signIn('dana@example.com', 'a-password').subscribe((r) => (received = r));

    http.expectOne('/api/Auth/login').flush(payload);

    expect(received).toEqual(payload);
  });

  it('posts both tokens to the refresh endpoint', () => {
    api.refresh('old-access-token', 'old-refresh-token').subscribe();

    const request = http.expectOne('/api/Auth/refresh');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      accessToken: 'old-access-token',
      refreshToken: 'old-refresh-token',
    });

    request.flush(payload);
  });

  it('posts the register payload with a null phone when blank (ASG-5)', () => {
    api
      .register({
        email: 'dana@example.com',
        username: 'dana',
        password: 'Password123',
        firstName: 'Dana',
        lastName: 'Support',
        phoneNumber: null,
      })
      .subscribe();

    const request = http.expectOne('/api/Auth/register');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'dana@example.com',
      username: 'dana',
      password: 'Password123',
      firstName: 'Dana',
      lastName: 'Support',
      phoneNumber: null,
    });

    request.flush({ id: 'u-1' });
  });

  it('sends the trimmed phone through register', () => {
    let receivedId: string | undefined;
    api
      .register({
        email: 'dana@example.com',
        username: 'dana',
        password: 'Password123',
        firstName: 'Dana',
        lastName: 'Support',
        phoneNumber: '+966555123456',
      })
      .subscribe((r) => (receivedId = r.id));

    http.expectOne('/api/Auth/register').flush({ id: 'u-9' });

    expect(receivedId).toBe('u-9');
  });
});
