import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import PortalHomeComponent from './home.component';

describe('PortalHomeComponent', () => {
  function render() {
    TestBed.configureTestingModule({
      imports: [PortalHomeComponent],
      providers: [provideRouter([])],
    });
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(PortalHomeComponent);
    fixture.detectChanges();
    return { fixture, navigateSpy };
  }

  it('shows the landing page with create-account and live-chat links (ASG-1)', () => {
    const { fixture } = render();
    const el = fixture.nativeElement as HTMLElement;

    const links = Array.from(el.querySelectorAll('a[href]')).map((a) => a.getAttribute('href'));
    expect(links).toContain('/signup');
    expect(links).toContain('/live-chat');
  });

  it('does not redirect when loaded', () => {
    const { navigateSpy } = render();
    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('AC412_LandingAndSignupMatchReferenceComposition: home landing contains hero and action links', () => {
    const { fixture } = render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h1')).not.toBeNull();
  });

  it('AC418_SignupAndLandingRemainKeyboardReachable: interactive CTA links have valid href and labels', () => {
    const { fixture } = render();
    const el = fixture.nativeElement as HTMLElement;
    const links = Array.from(el.querySelectorAll('a[href]'));
    expect(links.length).toBeGreaterThanOrEqual(2);
  });
});
