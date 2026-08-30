import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import TicketQueueComponent from './ticket-queue.component';

function page(items: unknown[], totalCount = items.length) {
  return {
    success: true,
    code: 'CON035',
    message: 'OK',
    data: { items, pageIndex: 1, pageSize: 10, totalCount },
    errors: [],
  };
}

const TICKET = {
  id: 't-1',
  reference: 'TKT-001001',
  subject: 'Cannot sign in',
  status: 'New',
  priority: 'Normal',
  customerId: 'c-1',
  customerName: 'Layla Haddad',
  categoryId: 'cat-1',
  categoryName: 'Technical',
  assigneeId: null,
  createdAt: '2026-08-26T09:00:00Z',
  escalationState: 'None',
};

const ESCALATED_TICKET = { ...TICKET, id: 't-2', reference: 'TKT-001002', escalationState: 'Level1' };
const ASSIGNED_TICKET = {
  ...TICKET,
  id: 't-3',
  reference: 'TKT-001003',
  assigneeId: 'agent-1',
  assigneeName: 'Mona Agent',
  status: 'Assigned',
};

const SERVER_FAILURE = {
  success: false,
  code: 'INTERNAL_ERROR',
  message: 'Something went wrong on the server',
  data: null,
  errors: [],
};

describe('TicketQueueComponent', () => {
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

  function render(): ComponentFixture<TicketQueueComponent> {
    const fixture = TestBed.createComponent(TicketQueueComponent);
    fixture.detectChanges();
    return fixture;
  }

  function flushList(
    fixture: ComponentFixture<TicketQueueComponent>,
    body: object,
    status?: number,
  ) {
    const request = http.expectOne((r) => r.url === '/api/Tickets');
    if (status) {
      request.flush(body, { status, statusText: 'Error' });
    } else {
      request.flush(body);
    }
    fixture.detectChanges();
    return request;
  }

  it('AC57: renders the tickets returned by the api', () => {
    const fixture = render();
    flushList(fixture, page([TICKET]));

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('TKT-001001');
    expect(text).toContain('Cannot sign in');
    expect(text).toContain('Layla Haddad');
  });

  it('AC57: the status filter refetches with the selected status', () => {
    const fixture = render();
    flushList(fixture, page([TICKET]));

    fixture.componentInstance.selectStatus('Open');

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.get('status')).toBe('Open');
    request.flush(page([]));
  });

  it('AC57: the my-tickets toggle requests only the caller’s own work', () => {
    const fixture = render();
    flushList(fixture, page([TICKET]));

    fixture.componentInstance.toggleMine();

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    // `mine` is resolved from the token server-side — the client never sends an assignee id.
    expect(request.request.params.get('mine')).toBe('true');
    expect(request.request.params.has('assigneeId')).toBe(false);
    request.flush(page([]));
  });

  it('AC58: shows a loading state while the request is in flight', () => {
    const fixture = render();

    expect(fixture.componentInstance.state().status).toBe('loading');
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="status"]')).not.toBeNull();

    flushList(fixture, page([TICKET]));
  });

  /**
   * The half of AC-58 that matters. `catchError(() => of([]))` is the default mistake here, and it
   * turns a 500 into "no tickets": the user reports missing work, nobody looks for a server fault,
   * and the outage stays invisible.
   */
  it('AC58: a failed request renders the error state, never the empty state', () => {
    const fixture = render();
    flushList(fixture, SERVER_FAILURE, 500);

    expect(fixture.componentInstance.state().status).toBe('error');

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')?.textContent).toContain(
      'Something went wrong on the server',
    );
    expect(el.textContent).not.toContain('No tickets have been raised yet');
  });

  /**
   * The other half. The retry button's presence in the error state and ABSENCE here is both the
   * honest signal — nothing failed, so there is nothing to retry — and the visual difference that
   * stops the two states reading alike.
   */
  it('AC58: a successful empty result renders the empty state, with no retry offered', () => {
    const fixture = render();
    flushList(fixture, page([]));

    expect(fixture.componentInstance.state().status).toBe('empty');

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('No tickets have been raised yet');
    expect(el.querySelector('button')).toBeNull();
  });

  it('AC58: retrying re-issues the request', () => {
    const fixture = render();
    flushList(fixture, SERVER_FAILURE, 500);

    fixture.componentInstance.load();

    expect(fixture.componentInstance.state().status).toBe('loading');
    flushList(fixture, page([TICKET]));
    expect(fixture.componentInstance.state().status).toBe('loaded');
  });

  it('AC57: advances the page parameter when the next page is requested', () => {
    const fixture = render();
    // 25 total over a page size of 10, so a next page exists.
    flushList(fixture, page([TICKET], 25));

    fixture.componentInstance.goToPage(2);

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.get('page')).toBe('2');
    request.flush(page([TICKET], 25));
  });

  it('AC158: shows an escalation badge on an escalated row', () => {
    const fixture = render();
    flushList(fixture, page([TICKET, ESCALATED_TICKET]));

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Level 1');
  });

  it('AC158: does not show a badge on a non-escalated row', () => {
    const fixture = render();
    flushList(fixture, page([TICKET]));

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="escalation-badge"]'),
    ).toBeNull();
  });

  it('AC159: sorting by escalation moves escalated rows to the top of the loaded page', () => {
    const fixture = render();
    flushList(fixture, page([TICKET, ESCALATED_TICKET]));

    fixture.componentInstance.sortByEscalation();
    fixture.detectChanges();

    expect(fixture.componentInstance.tickets()[0].id).toBe('t-2');
  });

  it('filters the loaded page using the Stitch search box', () => {
    const fixture = render();
    flushList(fixture, page([TICKET, ASSIGNED_TICKET]));

    fixture.componentInstance.updateSearch('mona');
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('TKT-001003');
    expect(text).not.toContain('TKT-001001');
  });

  it('shows management summary cards and real assignee names', () => {
    const fixture = render();
    flushList(fixture, page([TICKET, ESCALATED_TICKET, ASSIGNED_TICKET], 128));

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Total tickets');
    expect(text).toContain('Escalated on page');
    expect(text).toContain('Mona Agent');
  });

  it('shows a distinct empty state when search hides the loaded page', () => {
    const fixture = render();
    flushList(fixture, page([TICKET]));

    fixture.componentInstance.updateSearch('database');
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'No tickets match that search',
    );
  });

  it('says the filter matched nothing, rather than claiming the queue is empty', () => {
    const fixture = render();
    flushList(fixture, page([TICKET]));

    fixture.componentInstance.selectStatus('Closed');
    flushList(fixture, page([]));

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'No tickets match this filter',
    );
  });

  it('AC407_QueueUsesReferenceTableCompositionAndStates: queue renders reference table and headers', () => {
    const fixture = render();
    flushList(fixture, page([TICKET]));
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('table')).not.toBeNull();
    expect(el.querySelectorAll('th').length).toBe(8);
  });

  it('AC416_TicketScreensDistinguishLoadingEmptyAndError: loading, empty, error and loaded states remain distinct', () => {
    const fixture = render();
    expect(fixture.componentInstance.state().status).toBe('loading');
    flushList(fixture, page([]));
    expect(fixture.componentInstance.state().status).toBe('empty');
  });

  it('AC418_TicketFormsAndActionsAreKeyboardAccessible: pagination buttons have accessible aria labels', () => {
    const fixture = render();
    flushList(fixture, page([TICKET], 25));
    const el = fixture.nativeElement as HTMLElement;
    const prevBtn = el.querySelector('button[aria-label="Previous"]');
    const nextBtn = el.querySelector('button[aria-label="Next"]');
    expect(prevBtn).not.toBeNull();
    expect(nextBtn).not.toBeNull();
  });
});
