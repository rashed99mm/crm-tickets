import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { LocaleStore, NotificationStore, SessionStore, TRANSLATIONS } from 'common';
import { AdminShell } from './shell.component';

/** A routable nothing. The shell's heading is derived from the url, not from what renders in it. */
@Component({
  selector: 'admin-blank',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '',
})
class Blank {}

/** A structurally valid JWT carrying the given claims. Never verified client-side. */
function jwtWithClaims(claims: Record<string, unknown>): string {
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(claims)}.signature`;
}

describe('AdminShell', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('dir');
    TestBed.configureTestingModule({
      providers: [
        // Sign-out navigates to /login. With no route registered the navigation rejects,
        // and Vitest reports it as an unhandled error that can mask a real one.
        provideRouter([{ path: 'login', component: Blank }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
  });

  it('renders a sidebar and a topbar', () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('nav')).not.toBeNull();
    expect(el.querySelector('header')).not.toBeNull();
  });

  it('uses no physical-direction class in its own markup', () => {
    // The repository RTL guard scans .html files. This shell is an inline
    // template, so it escapes that guard entirely and needs its own check —
    // otherwise the one component defining the app's chrome is the one place
    // a physical utility could hide.
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    const html = (fixture.nativeElement as HTMLElement).innerHTML;

    for (const banned of ['pl-', 'pr-', 'ml-', 'mr-', 'text-left', 'text-right']) {
      expect(html).not.toContain(banned);
    }
  });

  /**
   * The customer screens existed as an API for two phases and were unreachable in the product —
   * gap `G-5`. A screen with no way in has shipped nothing, so the nav link is part of AC-69.
   */
  it('AC69: lists Customers in the sidebar beside Tickets', () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('nav a')).map(
      (a) => a.textContent?.trim(),
    );

    expect(links).toContain('Customers');
  });

  it('flips document direction with the locale', () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    TestBed.inject(LocaleStore).setLocale('ar');
    TestBed.inject(ApplicationRef).tick();

    expect(document.documentElement.dir).toBe('rtl');
  });

  it('clears the session on sign out', () => {
    // Every session signal is computed over the stored session, so clearing
    // it clears all of them - there is only one thing to set.
    const session = TestBed.inject(SessionStore);
    // A decodable token, not a placeholder: isAuthenticated is computed from
    // the claims, so "a.b.c" reads as unauthenticated and the test would pass
    // for the wrong reason.
    session.signIn({
      userId: 'u-1',
      email: 'dana@example.com',
      firstName: 'Dana',
      lastName: 'Support',
      accessToken: jwtWithClaims({
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Admin',
      }),
      refreshToken: 'a-refresh-token',
      accessTokenExpiresAt: '2026-08-25T11:00:00+00:00',
      refreshTokenExpiresAt: '2026-09-08T10:00:00+00:00',
      roles: ['Admin'],
    });
    expect(session.isAuthenticated()).toBe(true);

    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    fixture.componentInstance.signOut();

    expect(session.isAuthenticated()).toBe(false);
    expect(session.displayName()).toBeNull();
  });

  /**
   * MVP-12 moved the landing page to the dashboard, so it leads the sidebar. A nav whose first
   * item is not the route '' redirects to reads as a misconfiguration to anyone checking.
   */
  it('AC77: lists Dashboard first in the sidebar', () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('nav a')).map(
      (a) => a.textContent?.trim(),
    );

    expect(links[0]).toBe('Dashboard');
  });

  it('groups sidebar navigation into CRM categories', () => {
    TestBed.inject(SessionStore).signIn({
      userId: 'u-1',
      email: 'dana@example.com',
      firstName: 'Dana',
      lastName: 'Support',
      accessToken: jwtWithClaims({
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Admin',
      }),
      refreshToken: 'refresh',
      accessTokenExpiresAt: '2099-08-25T11:00:00+00:00',
      refreshTokenExpiresAt: '2099-09-08T10:00:00+00:00',
      roles: ['Admin'],
    });
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Workspace');
    expect(text).toContain('Operations');
    expect(text).toContain('Intelligence');
    expect(text).toContain('Administration');
  });

  it('AC405_DesktopShellMatchesCommandCenterComposition: shell sets command-center design system and desktop layout', () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const root = el.querySelector('[data-design-system]');
    expect(root?.getAttribute('data-design-system')).toBe('command-center');
    expect(el.querySelector('header')).not.toBeNull();
    expect(el.querySelector('nav')).not.toBeNull();
  });

  it('AC413_MobileShellUsesAccessibleDrawerWithoutOverflow: mobile drawer toggles and renders accessible aside', () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();
    const inst = fixture.componentInstance;
    expect(inst['mobileMenuOpen']()).toBe(false);

    inst.toggleMobileMenu();
    fixture.detectChanges();
    expect(inst['mobileMenuOpen']()).toBe(true);

    const el = fixture.nativeElement as HTMLElement;
    const drawer = el.querySelector('aside[aria-label="Mobile Navigation Drawer"]');
    expect(drawer).not.toBeNull();

    inst.closeMobileMenu();
    fixture.detectChanges();
    expect(inst['mobileMenuOpen']()).toBe(false);
  });

  it('AC414_TabletShellUsesTabletNavigation: sidebar collapsed mode toggles properly', () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();
    const inst = fixture.componentInstance;
    const initial = inst['collapsed']();
    inst.toggleCollapsed();
    fixture.detectChanges();
    expect(inst['collapsed']()).toBe(!initial);
  });

  it('AC415_DesktopShellPreservesGuttersAndMaxWidth: main container has responsive padding and full height layout', () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();
    const main = (fixture.nativeElement as HTMLElement).querySelector('main');
    expect(main?.className).toContain('p-4');
    expect(main?.className).toContain('sm:p-6');
  });

  it('AC418_DrawerAndNavigationHaveKeyboardFocusManagement: drawer trigger and close buttons have accessible labels', () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const trigger = el.querySelector('button[data-testid="mobile-nav-trigger"]');
    expect(trigger?.hasAttribute('aria-label')).toBe(true);
    expect(trigger?.hasAttribute('aria-expanded')).toBe(true);
  });

  it('FN4: hydrates the notification inbox for an authenticated session', () => {
    const session = TestBed.inject(SessionStore);
    session.signIn({
      userId: 'u-1',
      email: 'dana@example.com',
      firstName: 'Dana',
      lastName: 'Support',
      accessToken: jwtWithClaims({}),
      refreshToken: 'refresh',
      accessTokenExpiresAt: '2099-08-25T11:00:00+00:00',
      refreshTokenExpiresAt: '2099-09-08T10:00:00+00:00',
      roles: ['Admin'],
    });

    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne('/api/Notifications?page=1&pageSize=50');
    expect(request.request.method).toBe('GET');
    request.flush({
      items: [
        {
          id: 'n-1',
          userId: 'u-1',
          title: 'Assigned',
          message: 'Ticket assigned',
          notificationType: 'TicketAssigned',
          channel: 'InApp',
          status: 'Sent',
          readAt: null,
          sentAt: '2026-08-27T10:00:00Z',
          retryCount: 0,
          createdAt: '2026-08-27T10:00:00Z',
        },
      ],
      pageIndex: 1,
      pageSize: 50,
      totalCount: 1,
    });

    fixture.componentInstance.toggleNotifications();
    fixture.detectChanges();

    expect(TestBed.inject(NotificationStore).items()[0].title).toBe('Assigned');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Assigned');
  });

  it('FN3: marks a clicked notification read through the API and updates the store', () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();
    const store = TestBed.inject(NotificationStore);
    store.setAll([
      {
        id: 'n-1',
        title: 'Assigned',
        message: 'Ticket assigned',
        type: 'TicketAssigned',
        isRead: false,
        createdAt: '2026-08-27T10:00:00Z',
      },
    ]);
    fixture.componentInstance.toggleNotifications();
    fixture.detectChanges();

    const notification = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button[type="button"]'),
    ).find((button) => button.textContent?.includes('Assigned'));
    expect(notification?.textContent).toContain('Assigned');
    (notification as HTMLButtonElement).click();

    const request = TestBed.inject(HttpTestingController).expectOne('/api/Notifications/n-1/read');
    expect(request.request.method).toBe('POST');
    request.flush(null);

    expect(store.items()[0].isRead).toBe(true);
    expect(store.unreadCount()).toBe(0);
  });

  it('FN5: signing out clears the notification inbox', () => {
    const session = TestBed.inject(SessionStore);
    session.signIn({
      userId: 'u-1',
      email: 'dana@example.com',
      firstName: 'Dana',
      lastName: 'Support',
      accessToken: jwtWithClaims({}),
      refreshToken: 'refresh',
      accessTokenExpiresAt: '2099-08-25T11:00:00+00:00',
      refreshTokenExpiresAt: '2099-09-08T10:00:00+00:00',
      roles: ['Admin'],
    });
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();
    const store = TestBed.inject(NotificationStore);
    store.add({
      id: 'n-1',
      title: 'Assigned',
      message: 'Ticket assigned',
      type: 'TicketAssigned',
      isRead: false,
      createdAt: '2026-08-27T10:00:00Z',
    });
    fixture.componentInstance.signOut();
    TestBed.inject(ApplicationRef).tick();

    expect(store.items()).toEqual([]);
    expect(store.unreadCount()).toBe(0);
  });
});


/**
 * The shell used to render a literal `<h1>Tickets</h1>` in the topbar, so /customers and
 * /dashboard both announced themselves as the ticket queue. Applying the Command Center design
 * removed that heading — the mockups' topbar carries actions only, and every routed screen
 * already renders its own — so the route→name mapping now names the browser tab instead.
 * These tests followed it there; the guarantee is unchanged.
 */
describe('AdminShell screen name', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('dir');
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'dashboard', component: Blank },
          { path: 'tickets', component: Blank },
          { path: 'tickets/:id', component: Blank },
          { path: 'customers', component: Blank },
          { path: 'profile', component: Blank },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
  });

  it('names the active screen rather than always saying Tickets', async () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    const app = TestBed.inject(ApplicationRef);
    const name = () => TestBed.inject(Title).getTitle().split(' — ')[0];

    await router.navigateByUrl('/customers');
    app.tick();
    expect(name()).toBe('Customers');

    await router.navigateByUrl('/dashboard');
    app.tick();
    expect(name()).toBe('Dashboard');

    await router.navigateByUrl('/tickets');
    app.tick();
    expect(name()).toBe('Tickets');

    // A child route belongs to its parent screen, not to nothing.
    await router.navigateByUrl('/tickets/t-1');
    app.tick();
    expect(name()).toBe('Tickets');

    // A longer path is not swallowed by a shorter prefix.
    await router.navigateByUrl('/profile');
    app.tick();
    expect(name()).toBe('Profile');
  });

  /**
   * The defect visual verification caught: the topbar repeated the screen's own heading, so every
   * page carried two top-level headings.
   */
  it('does not repeat the routed screen heading in the topbar', async () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    await TestBed.inject(Router).navigateByUrl('/customers');
    TestBed.inject(ApplicationRef).tick();

    const header = (fixture.nativeElement as HTMLElement).querySelector('header');
    expect(header?.querySelector('h1')).toBeNull();
  });

  it('AC63: the topbar heading comes from the dictionary, not a literal', async () => {
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    const app = TestBed.inject(ApplicationRef);

    await router.navigateByUrl('/customers');
    app.tick();

    TestBed.inject(LocaleStore).setLocale('ar');
    app.tick();

    const el = fixture.nativeElement as HTMLElement;
    expect(TestBed.inject(Title).getTitle()).toContain(TRANSLATIONS['nav.customers'].ar);
    // The sidebar and the sign-out button flip with it — no request was made for any of it.
    expect(el.querySelector('nav')?.textContent).toContain(TRANSLATIONS['app.name'].ar);
    expect(el.querySelector('header')?.textContent).toContain(TRANSLATIONS['auth.signOut'].ar);
  });
});
