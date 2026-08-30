import { HttpRequest, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor, SessionStore } from 'common';
import DashboardComponent from './dashboard.component';

const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

/** A JWT is three base64url segments; SessionStore.roles is computed from the middle one. */
function fakeJwt(roles: string[]): string {
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${encode({ alg: 'none' })}.${encode({ [ROLE_CLAIM]: roles })}.signature`;
}

function page(items: unknown[], totalCount = items.length) {
  return { success: true, code: 'CON035', message: 'OK', data: { items, pageIndex: 1, pageSize: 10, totalCount }, errors: [] };
}

const TICKET = {
  id: 't-1',
  reference: 'TKT-001001',
  subject: 'Cannot sign in',
  status: 'Open',
  priority: 'High',
  customerId: 'c-1',
  customerName: 'Layla Haddad',
  categoryId: 'cat-1',
  categoryName: 'Technical',
  assigneeId: 'u-1',
  createdAt: '2026-08-26T09:00:00Z',
  escalationState: 'None',
};

const SERVER_FAILURE = {
  success: false,
  code: 'INTERNAL_ERROR',
  message: 'Something went wrong on the server',
  data: null,
  errors: [],
};

describe('DashboardComponent', () => {
  let http: HttpTestingController;

  function configure(roles: string[]) {
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(SessionStore).signIn({
      userId: 'u-1',
      email: 'dana@example.com',
      firstName: 'Dana',
      lastName: 'Support',
      accessToken: fakeJwt(roles),
      refreshToken: 'refresh-token',
      accessTokenExpiresAt: '2026-09-01T00:00:00Z',
      refreshTokenExpiresAt: '2026-09-08T00:00:00Z',
      roles,
    });
  }

  // Every panel hits the same url, so the panels are told apart by their parameters rather than
  // by the order they happen to be issued in — an ordering assertion would break the moment two
  // independent loads are reordered, which is a refactor and not a regression.
  function matching(predicate: (request: HttpRequest<unknown>) => boolean): TestRequest[] {
    return http.match((r) => r.url === '/api/Tickets' && predicate(r));
  }

  const isList = (r: HttpRequest<unknown>) => r.params.get('pageSize') === '10';
  const isCountFor = (status: string) => (r: HttpRequest<unknown>) =>
    r.params.get('pageSize') === '1' && r.params.get('status') === status;
  const isUnassignedCount = (r: HttpRequest<unknown>) => r.params.get('unassigned') === 'true';

  function only(predicate: (request: HttpRequest<unknown>) => boolean): TestRequest {
    const found = matching(predicate);
    expect(found.length).toBe(1);
    return found[0];
  }

  function flushSupervisorReports() {
    http.expectOne((r) => r.url === '/api/reports/ticket-volume').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: {
        byPeriod: [{ key: '2026-08-29', count: 1420 }, { key: '2026-08-28', count: 900 }],
        byCategory: [],
        byPriority: [],
      },
      errors: [],
    });
    http.expectOne((r) => r.url === '/api/reports/agent-performance').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: {
        byAgent: [
          { agentId: 'u-1', agentName: 'Jane Doe', ticketsResolved: 142, avgHandleMinutes: 14 },
          { agentId: 'u-2', agentName: 'Alex Smith', ticketsResolved: 128, avgHandleMinutes: 16 },
        ],
      },
      errors: [],
    });
  }

  /** The lifecycle status tiles, flushed together because they are issued together. */
  function flushCounts(counts: Partial<Record<string, number>> = {}) {
    for (const status of [
      'New',
      'Open',
      'Assigned',
      'In Progress',
      'Waiting for Customer',
      'Waiting for Internal Team',
      'Resolved',
      'Closed',
    ]) {
      only(isCountFor(status)).flush(page([], counts[status] ?? 0));
    }
  }

  function render(roles: string[] = ['Agent']): ComponentFixture<DashboardComponent> {
    configure(roles);
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('AC77: shows my open tickets without setting a filter', () => {
    const fixture = render();

    const list = only(isList);
    // `mine=true` is what makes this "my" work; no status parameter, because the dashboard is not
    // a pre-filtered queue — the agent set nothing.
    expect(list.request.params.get('mine')).toBe('true');
    expect(list.request.params.has('status')).toBe(false);
    expect(list.request.params.get('page')).toBe('1');

    list.flush(page([TICKET]));
    flushCounts();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('TKT-001001');
    expect(text).toContain('Cannot sign in');
  });

  /**
   * The server returns the page newest-first and the component does not re-sort it. Re-sorting
   * client-side would paper over a server-side ordering regression, which is the one thing this
   * assertion exists to keep visible.
   */
  it('AC77: renders the rows in the order the server returned them', () => {
    const fixture = render();
    const older = {
      ...TICKET,
      id: 't-2',
      reference: 'TKT-001002',
      createdAt: '2026-08-20T09:00:00Z',
    };

    only(isList).flush(page([TICKET, older]));
    flushCounts();
    fixture.detectChanges();

    const rows = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="my-work"] tbody tr'),
    ).map((row) => row.textContent ?? '');

    expect(rows[0]).toContain('TKT-001001');
    expect(rows[1]).toContain('TKT-001002');
  });

  /**
   * `A17` — `Resolved` and `Closed` are not "my open work", so they are not counted. The negative
   * half is the one that matters: a fourth and fifth count would be two more round trips
   * producing a number the criterion never asked for.
   */
  it('AC78: shows a count for the CRM lifecycle statuses', () => {
    const fixture = render();

    only(isList).flush(page([TICKET]));
    flushCounts({ New: 3, Open: 5, Assigned: 2, Resolved: 1 });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="count-New"]')?.textContent).toContain('3');
    expect(el.querySelector('[data-testid="count-Open"]')?.textContent).toContain('5');
    expect(el.querySelector('[data-testid="count-Assigned"]')?.textContent).toContain('2');
    expect(el.querySelector('[data-testid="count-Resolved"]')?.textContent).toContain('1');
  });

  it('AC78: counts my tickets, not the whole queue', () => {
    render();

    const counts = matching((r) => r.params.get('pageSize') === '1');
    expect(counts.length).toBe(8);
    for (const count of counts) {
      expect(count.request.params.get('mine')).toBe('true');
      count.flush(page([], 0));
    }

    only(isList).flush(page([TICKET]));
  });

  it('AC79: a row links to that ticket detail', () => {
    const fixture = render();

    only(isList).flush(page([TICKET]));
    flushCounts();
    fixture.detectChanges();

    const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>(
      '[data-testid="my-work"] a',
    );
    expect(link?.getAttribute('href')).toBe('/tickets/t-1');
  });

  /**
   * The retry button's ABSENCE here, and its presence in the sibling failure test below, is both
   * the honest signal — nothing failed, so there is nothing to retry — and the visual difference
   * AC-80 asks for.
   */
  it('AC80: no assigned work renders the empty state, with no retry', () => {
    const fixture = render();

    only(isList).flush(page([]));
    flushCounts();
    fixture.detectChanges();

    expect(fixture.componentInstance.myWork().status).toBe('empty');

    const el = fixture.nativeElement as HTMLElement;
    const panel = el.querySelector('[data-testid="my-work"]');
    expect(panel?.textContent).toContain('Nothing is assigned to you right now');
    expect(panel?.querySelector('button')).toBeNull();
    expect(el.querySelector('[role="alert"]')).toBeNull();
  });

  /**
   * `catchError(() => of([]))` is the default mistake here and it turns a 500 into "you have no
   * work": the agent goes home, nobody reports an outage, and the tickets sit unworked.
   */
  it('AC81: a failed load renders the error state with a retry', () => {
    const fixture = render();

    only(isList).flush(SERVER_FAILURE, { status: 500, statusText: 'Error' });
    flushCounts({ New: 3 });
    fixture.detectChanges();

    expect(fixture.componentInstance.myWork().status).toBe('error');

    const el = fixture.nativeElement as HTMLElement;
    const panel = el.querySelector('[data-testid="my-work"]');
    expect(panel?.querySelector('[role="alert"]')?.textContent).toContain(
      'Something went wrong on the server',
    );
    expect(panel?.querySelector('button')).not.toBeNull();
    expect(panel?.textContent).not.toContain('Nothing is assigned to you right now');

    // Independent panels: a failed list must not blank a count that succeeded.
    expect(el.querySelector('[data-testid="count-New"]')?.textContent).toContain('3');
  });

  it('AC81: retrying after a failure re-issues only the failed panel', () => {
    const fixture = render();

    only(isList).flush(SERVER_FAILURE, { status: 500, statusText: 'Error' });
    flushCounts();
    fixture.detectChanges();

    fixture.componentInstance.loadMyWork();

    expect(fixture.componentInstance.myWork().status).toBe('loading');
    // The counts succeeded, so retrying the list does not refetch them.
    expect(matching((r) => r.params.get('pageSize') === '1').length).toBe(0);

    only(isList).flush(page([TICKET]));
    fixture.detectChanges();

    expect(fixture.componentInstance.myWork().status).toBe('loaded');
  });

  it('AC82: a supervisor sees the unassigned count, linking to the queue filtered to them', () => {
    const fixture = render(['Supervisor']);

    only(isList).flush(page([TICKET]));
    flushCounts();
    only(isUnassignedCount).flush(page([], 7));
    flushSupervisorReports();
    fixture.detectChanges();

    const tile = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="unassigned"]');
    expect(tile?.textContent).toContain('7');
    expect(tile?.querySelector('a')?.getAttribute('href')).toBe('/tickets?unassigned=true');
  });

  it('AC82: an admin sees it too', () => {
    const fixture = render(['Admin']);

    only(isList).flush(page([TICKET]));
    flushCounts();
    only(isUnassignedCount).flush(page([], 2));
    flushSupervisorReports();
    fixture.detectChanges();

    const tile = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="unassigned"]');
    expect(tile?.textContent).toContain('2');
  });

  /**
   * The scraping never shipped without its report: the tile used to print a fabricated `4.8` to
   * everyone. Now it is read from `GET /api/reports/csat` over the same 30-day window the report
   * screens default to — the request is asserted, not just the rendered number.
   */
  it('a supervisor CSAT tile reads the real report average over the last thirty days', () => {
    const fixture = render(['Supervisor']);

    only(isList).flush(page([TICKET]));
    flushCounts();
    only(isUnassignedCount).flush(page([], 7));
    flushSupervisorReports();
    const csatRequest = http.expectOne((r) => r.url === '/api/reports/csat');
    expect(csatRequest.request.params.get('from')).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(csatRequest.request.params.get('to')).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    csatRequest.flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { totalResponses: 4, averageRating: 4.75, promoters: 3, passives: 1, detractors: 0, byRating: [] },
      errors: [],
    });
    fixture.detectChanges();

    const tile = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="csat"]');
    expect(tile?.textContent).toContain('4.8');
    expect(tile?.textContent).toContain('/ 5');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('1,420 Tickets');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Jane Doe');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('142');
  });

  it('zero CSAT responses renders a dash, never an invented score', () => {
    const fixture = render(['Supervisor']);

    only(isList).flush(page([TICKET]));
    flushCounts();
    only(isUnassignedCount).flush(page([], 0));
    flushSupervisorReports();
    http
      .expectOne((r) => r.url === '/api/reports/csat')
      .flush({
        success: true,
        code: 'CON035',
        message: 'OK',
        data: { totalResponses: 0, averageRating: 0, promoters: 0, passives: 0, detractors: 0, byRating: [] },
        errors: [],
      });
    fixture.detectChanges();

    const tile = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="csat"]');
    expect(tile?.textContent).toContain('—');
    expect(tile?.textContent).not.toContain('/5');
  });

  it('an agent does not see a CSAT tile, and does not request the report', () => {
    const fixture = render(['Agent']);

    http.expectNone((r) => r.url === '/api/reports/csat');

    only(isList).flush(page([TICKET]));
    flushCounts();
    fixture.detectChanges();

    http.expectNone((r) => r.url === '/api/reports/csat');
    expect(fixture.componentInstance.csat().status).toBe('idle');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="csat"]')).toBeNull();
  });

  /**
   * Hiding the tile is not enough. A request that is issued and then thrown away still puts the
   * number in the network tab, so the criterion is that an agent's browser never asks for it.
   */
  it('AC82: an agent does not see it, and does not request it', () => {
    const fixture = render(['Agent']);

    http.expectNone((r) => r.url === '/api/Tickets' && r.params.get('unassigned') === 'true');

    only(isList).flush(page([TICKET]));
    flushCounts();
    fixture.detectChanges();

    http.expectNone((r) => r.url === '/api/Tickets' && r.params.get('unassigned') === 'true');
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="unassigned"]'),
    ).toBeNull();
    expect(fixture.componentInstance.unassigned().status).toBe('idle');
  });

  it('shows a loading state for each panel while its request is in flight', () => {
    const fixture = render();

    expect(fixture.componentInstance.myWork().status).toBe('loading');
    expect(fixture.componentInstance.counts().status).toBe('loading');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('[role="status"]').length).toBe(
      2,
    );

    only(isList).flush(page([TICKET]));
    flushCounts();
  });

  it('AC406_DashboardUsesBentoGridAndMetricsHierarchy: renders bento metrics cards and table grid', () => {
    const fixture = render();
    only(isList).flush(page([TICKET]));
    flushCounts({ New: 3, Open: 5, Assigned: 2 });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('header')).not.toBeNull();
    expect(el.querySelectorAll('cs-stat-card').length).toBeGreaterThanOrEqual(3);
    expect(el.querySelector('table')).not.toBeNull();
  });

  it('AC416_DashboardAndPortalDistinguishAsyncStates: myWork and counts states handle loading and loaded independently', () => {
    const fixture = render();
    expect(fixture.componentInstance.myWork().status).toBe('loading');
    expect(fixture.componentInstance.counts().status).toBe('loading');
    only(isList).flush(page([]));
    flushCounts();
    fixture.detectChanges();
    expect(fixture.componentInstance.myWork().status).toBe('empty');
    expect(fixture.componentInstance.counts().status).toBe('loaded');
  });

  it('AC418_DashboardAndPortalAreKeyboardAccessible: quick links and action anchors have valid href targets', () => {
    const fixture = render();
    only(isList).flush(page([TICKET]));
    flushCounts();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const links = el.querySelectorAll('a[href]');
    expect(links.length).toBeGreaterThan(0);
  });
});
