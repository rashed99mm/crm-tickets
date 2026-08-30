import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import ReportsOverviewComponent from './reports-overview.component';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

const SERVER_FAILURE = {
  success: false,
  code: 'INTERNAL_ERROR',
  message: 'Something went wrong on the server',
  data: null,
  errors: [],
};

describe('ReportsOverviewComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function render(): ComponentFixture<ReportsOverviewComponent> {
    const fixture = TestBed.createComponent(ReportsOverviewComponent);
    fixture.detectChanges();
    return fixture;
  }

  function expectRequested(url: string) {
    const request = http.expectOne((r) => r.url === url);
    expect(request.request.params.get('from')).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(request.request.params.get('to')).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    return request;
  }

  /** The four report endpoints a fresh overview always asks for. */
  function flushHappy() {
    expectRequested('/api/reports/ticket-volume').flush(
      ok({
        byPeriod: [
          { key: '2026-08-01', count: 3 },
          { key: '2026-08-02', count: 7 },
        ],
        byCategory: [],
        byPriority: [],
      }),
    );
    expectRequested('/api/reports/sla-performance').flush(
      ok({
        byPriority: [
          {
            priority: 'High',
            total: 5,
            metFirstResponse: 4,
            breachedFirstResponse: 1,
            metResolution: 4,
            breachedResolution: 1,
          },
        ],
      }),
    );
    expectRequested('/api/reports/agent-performance').flush(
      ok({
        byAgent: [
          { agentId: 'a-1', agentName: 'Layla Haddad', ticketsResolved: 7, avgHandleMinutes: 30 },
          { agentId: 'a-2', agentName: 'Omar Khalil', ticketsResolved: 3, avgHandleMinutes: 90 },
        ],
      }),
    );
    expectRequested('/api/reports/csat').flush(
      ok({ totalResponses: 4, averageRating: 4.75, promoters: 3, passives: 1, detractors: 0, byRating: [] }),
    );
  }

  it('renders KPI values derived from the four report endpoints', () => {
    const fixture = render();
    flushHappy();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="kpi-volume"]')?.textContent).toContain('10');
    expect(el.querySelector('[data-testid="kpi-sla"]')?.textContent).toContain('20.0%');
    expect(el.querySelector('[data-testid="kpi-resolution"]')?.textContent).toContain('1h');
    expect(el.querySelector('[data-testid="kpi-csat"]')?.textContent).toContain('4.8');

    // The KPI cards must not be the only surfaces — the trend and the leaderboard render too.
    expect(el.querySelector('[data-testid="trend"]')?.textContent).toContain('7');
    expect(el.querySelector('[data-testid="leaderboard"]')?.textContent).toContain('Layla Haddad');
  });

  it('guards against a fabricated number: no survey responses renders a dash, not a score', () => {
    const fixture = render();

    expectRequested('/api/reports/ticket-volume').flush(ok({ byPeriod: [], byCategory: [], byPriority: [] }));
    expectRequested('/api/reports/sla-performance').flush(ok({ byPriority: [] }));
    expectRequested('/api/reports/agent-performance').flush(ok({ byAgent: [] }));
    expectRequested('/api/reports/csat').flush(
      ok({ totalResponses: 0, averageRating: 0, promoters: 0, passives: 0, detractors: 0, byRating: [] }),
    );
    fixture.detectChanges();

    const csat = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="kpi-csat"]');
    expect(csat?.textContent).toContain('—');
    expect(csat?.textContent).not.toContain('/5');
  });

  it('keeps panels independent: a failing trend blanks its own panel, not the leaderboard', () => {
    const fixture = render();

    expectRequested('/api/reports/ticket-volume').flush(SERVER_FAILURE, { status: 500, statusText: 'Error' });
    expectRequested('/api/reports/sla-performance').flush(SERVER_FAILURE, { status: 500, statusText: 'Error' });
    expectRequested('/api/reports/agent-performance').flush(
      ok({ byAgent: [{ agentId: 'a-1', agentName: 'Layla Haddad', ticketsResolved: 7, avgHandleMinutes: 30 }] }),
    );
    expectRequested('/api/reports/csat').flush(
      ok({ totalResponses: 1, averageRating: 5, promoters: 1, passives: 0, detractors: 0, byRating: [] }),
    );
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="trend"]')?.querySelector('[role="alert"]')?.textContent).toContain(
      'Something went wrong on the server',
    );
    expect(el.querySelector('[data-testid="trend"]')?.querySelector('button')).not.toBeNull();
    expect(el.querySelector('[data-testid="leaderboard"]')?.textContent).toContain('Layla Haddad');
  });

  it('offers every report screen as a one-hop link', () => {
    const fixture = render();
    flushHappy();
    fixture.detectChanges();

    const hrefs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('nav a')).map((a) =>
      (a as HTMLAnchorElement).getAttribute('href'),
    );
    expect(hrefs).toEqual(
      expect.arrayContaining([
        '/reports/ticket-volume',
        '/reports/sla-performance',
        '/reports/agent-performance',
        '/reports/csat',
        '/reports/live-queue',
      ]),
    );
  });
});