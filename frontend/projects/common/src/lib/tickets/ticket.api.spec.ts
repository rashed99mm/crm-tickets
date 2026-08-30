import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from '../api/envelope.interceptor';
import { PERMITTED_TRANSITIONS, TicketApi, TICKET_STATUSES } from './ticket.api';

describe('TicketApi', () => {
  let api: TicketApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    api = TestBed.inject(TicketApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts a create to /api/Tickets with the payload the backend expects', () => {
    api.create({
      subject: 'Cannot sign in',
      description: 'The portal rejects my password.',
      customerId: 'c-1',
      categoryId: 'cat-1',
      priority: 'Normal',
    }).subscribe();

    const request = http.expectOne('/api/Tickets');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      subject: 'Cannot sign in',
      description: 'The portal rejects my password.',
      customerId: 'c-1',
      categoryId: 'cat-1',
      priority: 'Normal',
    });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });
  });

  it('sends the filters as query parameters', () => {
    api.list({ page: 2, pageSize: 25, status: 'Open', mine: true }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('status')).toBe('Open');
    expect(request.request.params.get('mine')).toBe('true');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { items: [], pageIndex: 2, pageSize: 25, totalCount: 0 }, errors: [] });
  });

  /**
   * A blank `status=` is not the same as no status: the backend refuses an unrecognised status
   * value with a 400 rather than matching nothing (AC-33), so an unset filter has to be absent.
   */
  it('omits an unset status rather than sending an empty one', () => {
    api.list({ status: null, mine: false }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.has('status')).toBe(false);
    expect(request.request.params.has('mine')).toBe(false);
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { items: [], pageIndex: 1, pageSize: 10, totalCount: 0 }, errors: [] });
  });

  it('unwraps the envelope so callers see the page, not the envelope', () => {
    let received: unknown;
    api.list().subscribe((page) => (received = page));

    http.expectOne((r) => r.url === '/api/Tickets').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { items: [{ id: 't-1' }], pageIndex: 1, pageSize: 10, totalCount: 1 },
      errors: [],
    });

    expect(received).toEqual({ items: [{ id: 't-1' }], pageIndex: 1, pageSize: 10, totalCount: 1 });
  });

  /**
   * `AC-82`'s supervisor tile. The same rule `status` follows: sent only when true. A blank
   * `unassigned=` is not the same as an absent one — the backend parses the filter value rather
   * than ignoring an empty string, so an unset flag has to be left off entirely.
   */
  it('AC82: sends unassigned only when it is true', () => {
    api.list({ unassigned: true }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.get('unassigned')).toBe('true');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { items: [], pageIndex: 1, pageSize: 10, totalCount: 0 }, errors: [] });
  });

  it('AC82: omits unassigned when it is false', () => {
    api.list({ unassigned: false }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.has('unassigned')).toBe(false);
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { items: [], pageIndex: 1, pageSize: 10, totalCount: 0 }, errors: [] });
  });

  /**
   * `AC-78` reads four totals and none of the rows. `pageSize=1` rather than `0` because zero is
   * a value the backend clamps rather than honours, and the row that does come back is discarded.
   */
  it('AC78: countOnly asks for a single row and yields the total', () => {
    let received: number | undefined;
    api.countOnly({ mine: true, status: 'Open' }).subscribe((total) => (received = total));

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('1');
    expect(request.request.params.get('status')).toBe('Open');
    expect(request.request.params.get('mine')).toBe('true');

    request.flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { items: [{ id: 't-1' }], pageIndex: 1, pageSize: 1, totalCount: 42 },
      errors: [],
    });

    expect(received).toBe(42);
  });

  describe('AC-532: 8-state status model', () => {
    it('StatusModel_ContainsExactlyEightStatuses', () => {
      expect(TICKET_STATUSES).toHaveLength(8);
      expect(TICKET_STATUSES).toContain('New');
      expect(TICKET_STATUSES).toContain('Open');
      expect(TICKET_STATUSES).toContain('Assigned');
      expect(TICKET_STATUSES).toContain('In Progress');
      expect(TICKET_STATUSES).toContain('Waiting for Customer');
      expect(TICKET_STATUSES).toContain('Waiting for Internal Team');
      expect(TICKET_STATUSES).toContain('Resolved');
      expect(TICKET_STATUSES).toContain('Closed');
    });

    it('StatusModel_PERMITTED_TRANSITIONS_Has12LegalPairs', () => {
      let pairCount = 0;
      for (const from of Object.keys(PERMITTED_TRANSITIONS) as (keyof typeof PERMITTED_TRANSITIONS)[]) {
        const targets = PERMITTED_TRANSITIONS[from];
        for (const _ of targets) {
          pairCount++;
        }
      }
      expect(pairCount).toBe(12);
    });

    it('TicketApi_PermittedTransitions_MatchesBackendTable', () => {
      expect(PERMITTED_TRANSITIONS['New']).toEqual(['Open']);
      expect(PERMITTED_TRANSITIONS['Open']).toEqual(['Assigned', 'Resolved']);
      expect(PERMITTED_TRANSITIONS['Assigned']).toEqual(['In Progress']);
      expect(PERMITTED_TRANSITIONS['In Progress']).toEqual(['Waiting for Customer', 'Waiting for Internal Team', 'Resolved']);
      expect(PERMITTED_TRANSITIONS['Waiting for Customer']).toEqual(['In Progress']);
      expect(PERMITTED_TRANSITIONS['Waiting for Internal Team']).toEqual(['In Progress']);
      expect(PERMITTED_TRANSITIONS['Resolved']).toEqual(['In Progress', 'Closed']);
      expect(PERMITTED_TRANSITIONS['Closed']).toEqual(['In Progress']);
    });
  });
});

describe('TicketApi', () => {
  let api: TicketApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    api = TestBed.inject(TicketApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts a create to /api/Tickets with the payload the backend expects', () => {
    api.create({
      subject: 'Cannot sign in',
      description: 'The portal rejects my password.',
      customerId: 'c-1',
      categoryId: 'cat-1',
      priority: 'Normal',
    }).subscribe();

    const request = http.expectOne('/api/Tickets');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      subject: 'Cannot sign in',
      description: 'The portal rejects my password.',
      customerId: 'c-1',
      categoryId: 'cat-1',
      priority: 'Normal',
    });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });
  });

  it('sends the filters as query parameters', () => {
    api.list({ page: 2, pageSize: 25, status: 'Open', mine: true }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('status')).toBe('Open');
    expect(request.request.params.get('mine')).toBe('true');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { items: [], pageIndex: 2, pageSize: 25, totalCount: 0 }, errors: [] });
  });

  /**
   * A blank `status=` is not the same as no status: the backend refuses an unrecognised status
   * value with a 400 rather than matching nothing (AC-33), so an unset filter has to be absent.
   */
  it('omits an unset status rather than sending an empty one', () => {
    api.list({ status: null, mine: false }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.has('status')).toBe(false);
    expect(request.request.params.has('mine')).toBe(false);
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { items: [], pageIndex: 1, pageSize: 10, totalCount: 0 }, errors: [] });
  });

  it('unwraps the envelope so callers see the page, not the envelope', () => {
    let received: unknown;
    api.list().subscribe((page) => (received = page));

    http.expectOne((r) => r.url === '/api/Tickets').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { items: [{ id: 't-1' }], pageIndex: 1, pageSize: 10, totalCount: 1 },
      errors: [],
    });

    expect(received).toEqual({ items: [{ id: 't-1' }], pageIndex: 1, pageSize: 10, totalCount: 1 });
  });

  /**
   * `AC-82`'s supervisor tile. The same rule `status` follows: sent only when true. A blank
   * `unassigned=` is not the same as an absent one — the backend parses the filter value rather
   * than ignoring an empty string, so an unset flag has to be left off entirely.
   */
  it('AC82: sends unassigned only when it is true', () => {
    api.list({ unassigned: true }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.get('unassigned')).toBe('true');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { items: [], pageIndex: 1, pageSize: 10, totalCount: 0 }, errors: [] });
  });

  it('AC82: omits unassigned when it is false', () => {
    api.list({ unassigned: false }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.has('unassigned')).toBe(false);
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { items: [], pageIndex: 1, pageSize: 10, totalCount: 0 }, errors: [] });
  });

  /**
   * `AC-78` reads four totals and none of the rows. `pageSize=1` rather than `0` because zero is
   * a value the backend clamps rather than honours, and the row that does come back is discarded.
   */
  it('AC78: countOnly asks for a single row and yields the total', () => {
    let received: number | undefined;
    api.countOnly({ mine: true, status: 'Open' }).subscribe((total) => (received = total));

    const request = http.expectOne((r) => r.url === '/api/Tickets');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('1');
    expect(request.request.params.get('status')).toBe('Open');
    expect(request.request.params.get('mine')).toBe('true');

    request.flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { items: [{ id: 't-1' }], pageIndex: 1, pageSize: 1, totalCount: 42 },
      errors: [],
    });

    expect(received).toBe(42);
  });
});
