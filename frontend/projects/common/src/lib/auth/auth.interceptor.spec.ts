import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { SessionStore } from './session.store';
import { TokenStorage } from './token-storage';

describe('authInterceptor', () => {
  let http: HttpClient;
  let mock: HttpTestingController;
  let session: SessionStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        SessionStore,
        TokenStorage,
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    mock = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionStore);
  });

  afterEach(() => mock.verify());

  it('sends no Authorization header when signed out', () => {
    http.get('/api/tickets').subscribe();

    expect(mock.expectOne('/api/tickets').request.headers.has('Authorization')).toBe(
      false,
    );
  });

  it('attaches the bearer token when signed in', () => {
    session.signIn({
      userId: 'u-1',
      email: 'dana@example.com',
      firstName: 'Dana',
      lastName: 'Support',
      accessToken: 'header.payload.sig',
      refreshToken: 'a-refresh-token',
      accessTokenExpiresAt: '2026-08-25T11:00:00+00:00',
      refreshTokenExpiresAt: '2026-09-08T10:00:00+00:00',
      roles: ['Admin'],
    });
    http.get('/api/tickets').subscribe();

    expect(mock.expectOne('/api/tickets').request.headers.get('Authorization')).toBe(
      'Bearer header.payload.sig',
    );
  });
});
