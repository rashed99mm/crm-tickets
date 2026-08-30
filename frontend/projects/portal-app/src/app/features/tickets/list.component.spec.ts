import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import PortalTicketListComponent from './list.component';

describe('PortalTicketListComponent', () => {
  let http: HttpTestingController;

  function setup() {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [PortalTicketListComponent],
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
    const fixture = TestBed.createComponent(PortalTicketListComponent);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => http.verify());

  function flushTickets(items: unknown[]) {
    http
      .expectOne((r) => r.url === '/api/portal/tickets')
      .flush({ success: true, code: 'CON035', message: 'OK', data: items, errors: [] });
  }

  it('loads the customer own tickets from /api/portal/tickets', () => {
    const fixture = create();
    const req = http.expectOne((r) => r.url === '/api/portal/tickets');
    expect(req.request.method).toBe('GET');

    req.flush({ success: true, code: 'CON035', message: 'OK', data: [
      { id: 't1', reference: 'TCK-1', subject: 'Login broken', status: 'Open', createdAt: '2026-01-01T00:00:00Z' },
    ], errors: [] });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('TCK-1');
    expect(el.textContent).toContain('Login broken');
  });

  it('shows the empty state when there are no tickets', () => {
    const fixture = create();
    flushTickets([]);
    fixture.detectChanges();

    expect(fixture.componentInstance.state().status).toBe('empty');
  });

  it('renders a three-column reference table for portal tickets', () => {
    const fixture = create();
    flushTickets([
      { id: 't1', reference: 'TCK-1', subject: 'Login broken', status: 'Open', createdAt: '2026-01-01T00:00:00Z' },
    ]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('table')).not.toBeNull();
    expect(el.querySelectorAll('th').length).toBe(3);
  });

  it('keeps loading, empty and loaded states distinct', () => {
    const fixture = create();
    expect(fixture.componentInstance.state().status).toBe('loading');
    flushTickets([]);
    fixture.detectChanges();
    expect(fixture.componentInstance.state().status).toBe('empty');
  });

  it('renders rows as keyboard-accessible links', () => {
    const fixture = create();
    flushTickets([
      { id: 't1', reference: 'TCK-1', subject: 'Login broken', status: 'Open', createdAt: '2026-01-01T00:00:00Z' },
    ]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('a[href]')).not.toBeNull();
  });
});
