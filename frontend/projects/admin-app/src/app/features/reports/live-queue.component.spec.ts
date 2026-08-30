import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor, TicketApi } from 'common';
import LiveQueueComponent from './live-queue.component';

const TICKET = (id: string, createdAt: string, assigneeId: string | null = null) => ({
  id,
  reference: `T-${id}`,
  subject: `Ticket ${id}`,
  status: 'Open',
  priority: 'High',
  customerId: 'c1',
  customerName: 'Acme',
  categoryId: 'cat1',
  categoryName: 'Billing',
  assigneeId,
  createdAt,
  escalationState: 'None',
});

describe('LiveQueueComponent', () => {
  let http: HttpTestingController;

  function setup() {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [LiveQueueComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  }

  function create() {
    setup();
    const fixture = TestBed.createComponent(LiveQueueComponent);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => http.verify());

  function flushAll(tickets: object[], agents: object[]) {
    const unassigned = http.expectOne(
      (r) => r.url === '/api/Tickets' && r.params.get('unassigned') === 'true',
    );
    unassigned.flush({ items: tickets, pageIndex: 1, pageSize: 50, totalCount: tickets.length });

    const open = http.expectOne((r) => r.url === '/api/Tickets' && r.params.get('status') === 'Open');
    open.flush({ items: tickets, pageIndex: 1, pageSize: 100, totalCount: tickets.length });

    const agentsReq = http.expectOne('/api/Tickets/assignable-agents');
    agentsReq.flush(agents);
  }

  it('flags the longest-waiting unassigned ticket as urgent and sorts oldest first (US-607)', () => {
    const fixture = create();
    // Older ticket (created 2h ago) should sort first and be urgent; newer one not urgent.
    const old = new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString();
    const fresh = new Date(Date.now() - 5 * 60 * 1000).toISOString();
    flushAll([TICKET('b', fresh), TICKET('a', old)], [{ id: 'ag1', name: 'Ada', email: 'a@x.com' }]);
    fixture.detectChanges();

    const rows = fixture.componentInstance.unassigned();
    expect(rows.length).toBe(2);
    expect(rows[0].id).toBe('a');
    expect(rows[0].urgent).toBe(true);
    expect(rows[1].urgent).toBe(false);
  });

  it('computes per-agent open load from assignable agents (US-607)', () => {
    const fixture = create();
    flushAll(
      [TICKET('a', new Date().toISOString(), 'ag1'), TICKET('b', new Date().toISOString(), 'ag1'), TICKET('c', new Date().toISOString(), 'ag2')],
      [
        { id: 'ag1', name: 'Ada', email: 'a@x.com' },
        { id: 'ag2', name: 'Bob', email: 'b@x.com' },
      ],
    );
    fixture.detectChanges();

    const load = fixture.componentInstance.agentLoad();
    expect(load.find((l) => l.agentId === 'ag1')?.openCount).toBe(2);
    expect(load.find((l) => l.agentId === 'ag2')?.openCount).toBe(1);
  });

  it('shows an error state when the queue cannot be loaded', () => {
    const fixture = create();
    // Match all three, settle the siblings FIRST, then error — forkJoin never cancels.
    const unassigned = http.expectOne(
      (r) => r.url === '/api/Tickets' && r.params.get('unassigned') === 'true',
    );
    const open = http.expectOne((r) => r.url === '/api/Tickets' && r.params.get('status') === 'Open');
    const agentsReq = http.expectOne('/api/Tickets/assignable-agents');
    open.flush({ items: [], pageIndex: 1, pageSize: 100, totalCount: 0 });
    agentsReq.flush([]);
    unassigned.error(new ProgressEvent('error'));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cs-error-state')).not.toBeNull();
  });
});
