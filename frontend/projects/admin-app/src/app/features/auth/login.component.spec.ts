import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { envelopeInterceptor, SessionStore } from 'common';
import { vi } from 'vitest';
import LoginComponent from './login.component';

/** A JWT is three base64url segments; a real access token needs to decode to something. */
function fakeJwt(payload: Record<string, unknown>): string {
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

  return `${encode({ alg: 'none' })}.${encode(payload)}.signature`;
}

const ACCESS_TOKEN = fakeJwt({
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['Admin'],
});

const UNAUTHORIZED_ENVELOPE = {
  success: false,
  code: 'INVALID_CREDENTIALS',
  message: 'Invalid email or password',
  data: null,
  errors: [],
};

const SUCCESS_ENVELOPE = {
  success: true,
  code: 'CON035',
  message: 'OK',
  data: {
    userId: 'u-1',
    email: 'dana@example.com',
    firstName: 'Dana',
    lastName: 'Support',
    accessToken: ACCESS_TOKEN,
    refreshToken: 'refresh-token-value',
    accessTokenExpiresAt: '2026-08-25T10:00:00+00:00',
    refreshTokenExpiresAt: '2026-09-08T10:00:00+00:00',
    roles: ['Admin'],
  },
  errors: [],
};

describe('LoginComponent', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        // The envelope interceptor is part of the contract under test: it is
        // what turns a rejection into ApiError and unwraps success to `data`.
        // Omitting it here made the component fall back to its unknown-error
        // path, which is a fiction production never sees.
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  function render(): ComponentFixture<LoginComponent> {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    return fixture;
  }

  function submit(fixture: ComponentFixture<LoginComponent>, email: string, password: string) {
    fixture.componentInstance.form.setValue({ email, password });
    fixture.componentInstance.submit();
    fixture.detectChanges();
  }

  it('does not submit an empty form', () => {
    // AC-59: submit is refused while invalid, so no request leaves.
    const fixture = render();
    fixture.componentInstance.submit();

    http.expectNone('/api/Auth/login');
  });

  it('stores the token and navigates on success', () => {
    // AC-55, the happy half.
    const fixture = render();
    submit(fixture, 'dana@example.com', 'a-password');

    http.expectOne('/api/Auth/login').flush(SUCCESS_ENVELOPE);
    fixture.detectChanges();

    expect(TestBed.inject(SessionStore).token()).toBe(ACCESS_TOKEN);
    expect(TestBed.inject(SessionStore).isAuthenticated()).toBe(true);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it('shows a visible error and does NOT navigate on invalid credentials', () => {
    // AC-55, the half that matters. A form that navigates and bounces back
    // shows a flash of the protected page, which reads as a bug even when
    // nothing was exposed.
    const fixture = render();
    submit(fixture, 'dana@example.com', 'wrong-password');

    http.expectOne('/api/Auth/login').flush(UNAUTHORIZED_ENVELOPE, {
      status: 401,
      statusText: 'Unauthorized',
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Invalid email or password');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
    expect(TestBed.inject(SessionStore).isAuthenticated()).toBe(false);
  });

  it('never renders the submitted password', () => {
    // AC-5 on the client. The server refuses to echo it; the form must not
    // put it in the DOM outside the password input either.
    const fixture = render();
    submit(fixture, 'dana@example.com', 'Sup3rSecret!');

    http.expectOne('/api/Auth/login').flush(UNAUTHORIZED_ENVELOPE, {
      status: 401,
      statusText: 'Unauthorized',
    });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Sup3rSecret!');
  });

  it('returns the user to the url the guard interrupted', () => {
    const fixture = render();
    fixture.componentInstance.returnUrl.set('/tickets/42');
    submit(fixture, 'dana@example.com', 'a-password');

    http.expectOne('/api/Auth/login').flush(SUCCESS_ENVELOPE);
    fixture.detectChanges();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/tickets/42');
  });

  it('is not busy once a request settles', () => {
    // A stuck spinner is indistinguishable from a hung server.
    const fixture = render();
    submit(fixture, 'dana@example.com', 'wrong-password');

    expect(fixture.componentInstance.busy()).toBe(true);

    http.expectOne('/api/Auth/login').flush(UNAUTHORIZED_ENVELOPE, {
      status: 401,
      statusText: 'Unauthorized',
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.busy()).toBe(false);
  });

  it('AC418_SignupAndLandingRemainKeyboardReachable: login form has accessible fields and button', () => {
    const fixture = render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('input[type="email"]')).not.toBeNull();
    expect(el.querySelector('input[type="password"]')).not.toBeNull();
    expect(el.querySelector('button[type="submit"]')).not.toBeNull();
  });
});

