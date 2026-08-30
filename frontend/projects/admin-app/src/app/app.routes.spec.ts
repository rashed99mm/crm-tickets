import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router } from '@angular/router';
import { SessionStore } from 'common';
import { routes } from './app.routes';

/** A structurally valid JWT. Never verified client-side; the claims drive UI affordances only. */
function jwtWithClaims(claims: Record<string, unknown>): string {
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(claims)}.signature`;
}

/** The `path` of the deepest route the URL actually matched. */
function matchedPath(router: Router): string | undefined {
  let node: ActivatedRouteSnapshot | null = router.routerState.snapshot.root;
  let path: string | undefined;

  while (node) {
    path = node.routeConfig?.path ?? path;
    node = node.firstChild;
  }

  return path;
}

describe('admin routes', () => {
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter(routes)],
    });
    router = TestBed.inject(Router);
  });

  function signIn(role = 'Admin'): void {
    TestBed.inject(SessionStore).signIn({
      userId: 'u-1',
      email: 'dana@example.com',
      firstName: 'Dana',
      lastName: 'Support',
      accessToken: jwtWithClaims({
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': role,
      }),
      refreshToken: 'a-refresh-token',
      accessTokenExpiresAt: '2026-09-01T00:00:00Z',
      refreshTokenExpiresAt: '2026-09-08T00:00:00Z',
      roles: [role],
    });
  }

  it('redirects an unauthenticated visit to a protected route', async () => {
    // AC-56.
    await router.navigateByUrl('/tickets');

    expect(router.url).toContain('/login');
  });

  it('carries the attempted url so the user returns to it', async () => {
    await router.navigateByUrl('/tickets');

    expect(router.url).toContain('returnUrl');
  });

  it('lets an unauthenticated visitor reach login', async () => {
    await router.navigateByUrl('/login');

    expect(router.url).toBe('/login');
  });

  it('sends an unknown url through the guard rather than 404ing into nothing', async () => {
    await router.navigateByUrl('/does-not-exist');

    expect(router.url).toContain('/login');
  });

  /**
   * Route order, not route presence. `customers/:id` declared first would swallow `/customers/new`
   * as a customer whose id is the literal string "new", and the failure is quiet: the detail screen
   * loads, asks the server for that id and renders a not-found state, which looks like a data
   * problem rather than a routing one.
   */
  it('AC70: /customers/new matches the create screen, not the detail screen', async () => {
    signIn();

    await router.navigateByUrl('/customers/new');

    expect(router.url).toBe('/customers/new');
    expect(matchedPath(router)).toBe('customers/new');
  });

  it('AC71: /customers/{id} matches the detail screen', async () => {
    signIn();

    await router.navigateByUrl('/customers/c-1');

    expect(router.url).toBe('/customers/c-1');
    expect(matchedPath(router)).toBe('customers/:id');
  });

  it('AC69: the customer list is behind the session guard like every other screen', async () => {
    await router.navigateByUrl('/customers');

    expect(router.url).toContain('/login');
  });

  /**
   * MVP-02 criterion 3: hiding the nav item is a courtesy; this guard (and the Admin policy on
   * `/api/Users` itself) is the control. A non-admin who reaches `/users` directly must not see
   * the staff screen — they land on `/forbidden` instead.
   */
  it('MVP02: a non-admin visiting /users is sent to /forbidden, not the staff screen', async () => {
    signIn('Agent');

    await router.navigateByUrl('/users');

    expect(router.url).toBe('/forbidden');
  });

  it('MVP02: an admin visiting /users reaches the staff screen', async () => {
    signIn('Admin');

    await router.navigateByUrl('/users');

    expect(router.url).toBe('/users');
  });

  it('MVP02: a non-admin visiting /permissions is sent to /forbidden', async () => {
    signIn('Agent');

    await router.navigateByUrl('/permissions');

    expect(router.url).toBe('/forbidden');
  });

  it('MVP02: an admin visiting /permissions reaches the permissions screen', async () => {
    signIn('Admin');

    await router.navigateByUrl('/permissions');

    expect(router.url).toBe('/permissions');
  });

});
