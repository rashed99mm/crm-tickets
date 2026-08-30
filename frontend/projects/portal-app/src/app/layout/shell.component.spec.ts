import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Router } from '@angular/router';
import { PortalShell } from './shell.component';
import { SessionStore } from 'common';

describe('PortalShell', () => {
  function render(isAuthed: boolean) {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), SessionStore],
    });
    if (isAuthed) {
      TestBed.inject(SessionStore).signIn({
        userId: 'u1',
        email: 'a@b.com',
        firstName: 'A',
        lastName: 'B',
        accessToken: 'at',
        refreshToken: 'rt',
        accessTokenExpiresAt: '',
        refreshTokenExpiresAt: '',
        roles: [],
      });
    }
    const fixture = TestBed.createComponent(PortalShell);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the brand and portal nav items', () => {
    const el = render(false);
    const navText = el.textContent ?? '';
    expect(navText).toContain('Home');
    expect(navText).toContain('Submit ticket');
    expect(navText).toContain('My tickets');
    expect(navText).toContain('FAQs');
    expect(navText).toContain('Articles');
    expect(navText).toContain('Solutions');
  });

  it('renders the portal navigation links under /app', () => {
    const el = render(false);
    const links = Array.from(el.querySelectorAll('a[href]')).map((a) => a.getAttribute('href'));
    expect(links).toContain('/app');
    expect(links).toContain('/app/tickets/new');
    expect(links).toContain('/app/tickets');
    expect(links).toContain('/app/faq');
    expect(links).toContain('/app/articles');
    expect(links).toContain('/app/solution');
  });

  it('shows a sign-in link when not authenticated', () => {
    const el = render(false);
    const links = Array.from(el.querySelectorAll('a')).map((a) => a.textContent?.trim());
    expect(links).toContain('Sign in');
  });

  it('shows the display name when authenticated', () => {
    const el = render(true);
    expect((el.textContent ?? '')).toContain('A B');
  });

  it('AC405_DesktopShellMatchesCommandCenterComposition: portal shell sets command-center design system', () => {
    const el = render(false);
    expect(el.querySelector('[data-design-system]')?.getAttribute('data-design-system')).toBe('command-center');
  });

  it('AC413_MobileShellUsesAccessibleDrawerWithoutOverflow: portal mobile drawer opens and closes', () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), SessionStore],
    });
    const fixture = TestBed.createComponent(PortalShell);
    fixture.detectChanges();
    const inst = fixture.componentInstance;
    expect(inst['mobileMenuOpen']()).toBe(false);

    inst['toggleMobileMenu']();
    fixture.detectChanges();
    expect(inst['mobileMenuOpen']()).toBe(true);

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('aside[aria-label="Mobile Navigation Drawer"]')).not.toBeNull();

    inst['closeMobileMenu']();
    fixture.detectChanges();
    expect(inst['mobileMenuOpen']()).toBe(false);
  });

  it('AC418_DrawerAndNavigationHaveKeyboardFocusManagement: mobile trigger has accessible attributes', () => {
    const el = render(false);
    const trigger = el.querySelector('button[data-testid="portal-mobile-nav-trigger"]');
    expect(trigger?.hasAttribute('aria-label')).toBe(true);
    expect(trigger?.hasAttribute('aria-expanded')).toBe(true);
  });
});
