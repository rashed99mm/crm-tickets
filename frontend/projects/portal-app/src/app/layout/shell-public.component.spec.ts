import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionStore } from 'common';
import { PortalPublicShell } from './shell-public.component';

describe('PortalPublicShell', () => {
  function render(isAuthed: boolean) {
    TestBed.configureTestingModule({
      imports: [PortalPublicShell],
      providers: [provideRouter([]), SessionStore],
    });
    if (isAuthed) {
      TestBed.inject(SessionStore).signIn({
        userId: 'u1',
        email: 'a@b.com',
        firstName: 'A',
        lastName: 'B',
        accessToken: `a.${btoa('{}')}.c`,
        refreshToken: 'rt',
        accessTokenExpiresAt: '',
        refreshTokenExpiresAt: '',
        roles: [],
      });
    }
    const fixture = TestBed.createComponent(PortalPublicShell);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the persistent navbar and footer around the routed content', () => {
    const fixture = render(false);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('header')).not.toBeNull();
    expect(el.querySelector('footer')).not.toBeNull();
    expect(el.querySelector('router-outlet, [router-outlet]')).not.toBeNull();
  });

  it('shows sign-in / create-account when logged out', () => {
    const fixture = render(false);
    const el = fixture.nativeElement as HTMLElement;
    const links = Array.from(el.querySelectorAll('a[href]')).map((a) => a.getAttribute('href'));
    expect(links).toContain('/login');
    expect(links).toContain('/signup');
    expect(links).toContain('/app/tickets/new');
  });

  it('shows the account area when authenticated', () => {
    const fixture = render(true);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('a[href="/app/profile"]')).not.toBeNull();
  });
});
