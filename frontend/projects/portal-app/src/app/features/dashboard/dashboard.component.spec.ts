import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionStore } from 'common';
import PortalDashboardComponent from './dashboard.component';

describe('PortalDashboardComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PortalDashboardComponent],
      providers: [provideRouter([]), SessionStore],
    });
  });

  it('renders quick action cards for tickets, submit, and knowledge base', () => {
    const fixture = TestBed.createComponent(PortalDashboardComponent);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('header')).not.toBeNull();
    const links = el.querySelectorAll('a');
    expect(links.length).toBeGreaterThanOrEqual(3);
  });

  it('AC412_PortalDashboardComposition: matches reference grid cards', () => {
    const fixture = TestBed.createComponent(PortalDashboardComponent);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('cs-card').length).toBe(3);
  });

  it('AC418_DashboardAndPortalAreKeyboardAccessible: action cards have valid router links', () => {
    const fixture = TestBed.createComponent(PortalDashboardComponent);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const hrefs = Array.from(el.querySelectorAll('a')).map((a) => a.getAttribute('href'));
    expect(hrefs).toContain('/app/tickets/new');
    expect(hrefs).toContain('/app/tickets');
    expect(hrefs).toContain('/app/kb');
  });
});
