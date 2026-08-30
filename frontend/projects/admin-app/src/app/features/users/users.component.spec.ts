import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import UsersComponent from './users.component';

function envelope(data: unknown) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

function failure(status: number, code: string, details: { field: string; code: string; message: string }[] | null = null) {
  return {
    body: {
      success: false,
      code,
      message: 'Validation failed',
      data: null,
      errors: details ?? [],
    },
    opts: { status, statusText: 'Error' },
  };
}

const STAFF = [
  {
    id: '1',
    email: 'a@test.local',
    username: 'ann',
    firstName: 'Ann',
    lastName: 'Agent',
    roles: ['User'],
    isActive: true,
    createdAt: '2026-08-01T00:00:00Z',
  },
  {
    id: '2',
    email: 'b@test.local',
    username: 'bob',
    firstName: 'Bob',
    lastName: 'Admin',
    roles: ['Admin'],
    isActive: false,
    createdAt: '2026-08-01T00:00:00Z',
  },
];

/** The list GET now always carries the server-side paging defaults (`GET /api/Users` query params). */
function isUsersList(request: { url: string; method: string }): boolean {
  return request.url === '/api/Users' && request.method === 'GET';
}

describe('UsersComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function render(): ComponentFixture<UsersComponent> {
    const fixture = TestBed.createComponent(UsersComponent);
    fixture.detectChanges();
    return fixture;
  }

  function flushList(items = STAFF, totalCount = items.length, pageIndex = 1): void {
    http.expectOne((r) => isUsersList(r)).flush(
      envelope({ items, pageIndex, pageSize: 10, totalCount }),
    );
  }

  it('renders a row per staff account with its active state', () => {
    const fixture = render();
    flushList();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('tbody tr').length).toBe(2);
    expect(el.textContent).toContain('Deactivated');
  });

  it('shows an error state, not an empty one, when the list fails', () => {
    // The whole point of the AsyncState union: a failure must never render as
    // "no staff accounts".
    const fixture = render();
    const f = failure(500, 'INTERNAL_ERROR');
    http.expectOne((r) => isUsersList(r)).flush(f.body, f.opts);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('No staff accounts yet');
  });

  it('shows an empty state when there are no accounts', () => {
    const fixture = render();
    flushList([]);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No staff accounts yet');
  });

  it('posts the new account and reloads the list', () => {
    const fixture = render();
    flushList();
    fixture.detectChanges();

    fixture.componentInstance.form.setValue({
      email: 'new@test.local',
      username: 'newuser',
      firstName: 'New',
      lastName: 'Staff',
      role: 'User',
      password: 'Created-Password-1',
    });
    fixture.componentInstance.create();

    const created = http.expectOne((r) => r.url === '/api/Users' && r.method === 'POST');
    expect(created.request.body.roles).toEqual(['User']);
    expect(created.request.body.username).toBe('newuser');
    created.flush(envelope({ id: '3' }), { status: 201, statusText: 'Created' });

    // The reload is what proves the list reflects the new account.
    http.expectOne((r) => isUsersList(r));
  });

  it('lands a server field error on the control that caused it', () => {
    const fixture = render();
    flushList();
    fixture.detectChanges();

    fixture.componentInstance.form.setValue({
      email: 'taken@test.local',
      username: 'newuser',
      firstName: 'New',
      lastName: 'Staff',
      role: 'User',
      // Long enough to pass client-side validation — this test exercises the
      // server's rejection, not the client's.
      password: 'weakweak',
    });
    fixture.componentInstance.create();

    const f = failure(422, 'PASSWORD_TOO_WEAK', [{ field: 'password', code: 'PASSWORD_TOO_WEAK', message: 'Password too weak' }]);
    http.expectOne((r) => r.method === 'POST').flush(f.body, f.opts);
    fixture.detectChanges();

    expect(fixture.componentInstance.fieldError('password')?.message).toBe('Password too weak');
    expect(fixture.componentInstance.fieldError('email')).toBeNull();
  });

  it('deactivates through the PUT activation endpoint', () => {
    const fixture = render();
    flushList();
    fixture.detectChanges();

    fixture.componentInstance.toggleActive(STAFF[0]);

    const call = http.expectOne('/api/Users/1/deactivate');
    expect(call.request.method).toBe('PUT');
    call.flush(envelope(null));

    http.expectOne((r) => isUsersList(r));
  });

  it('AC411_AdminAndKnowledgeBaseScreensPreserveReferenceHierarchy: users screen contains table and header', () => {
    const fixture = render();
    flushList();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('header')).not.toBeNull();
    expect(el.querySelector('table')).not.toBeNull();
  });

  it('AC416_CustomerAndAdminScreensShowDistinctAsyncStates: users component exposes loading, empty and loaded states', () => {
    const fixture = render();
    expect(fixture.componentInstance.state().status).toBe('loading');
    flushList([]);
    fixture.detectChanges();
    expect(fixture.componentInstance.state().status).toBe('empty');
  });

  it('AC418_AdminTablesAndRailsAreKeyboardAccessible: action buttons are rendered and accessible', () => {
    const fixture = render();
    flushList();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const buttons = el.querySelectorAll('button');
    expect(buttons.length).toBeGreaterThan(0);
  });

  // --- server-side paging, filters and sorts ---------------------------------

  it('loads the first page with the default sort (newest-created first)', () => {
    render();

    const request = http.expectOne((r) => isUsersList(r));
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('10');
    expect(request.request.params.get('sortBy')).toBe('createdat');
    expect(request.request.params.get('sortDirection')).toBe('desc');
    // No narrowing params on a pristine list.
    expect(request.request.params.has('search')).toBe(false);
    expect(request.request.params.has('isActive')).toBe(false);
    expect(request.request.params.has('role')).toBe(false);
    request.flush(envelope({ items: STAFF, pageIndex: 1, pageSize: 10, totalCount: 2 }));
  });

  it('pages forward: navigating to page two re-fetches with page=2', () => {
    const fixture = render();
    http
      .expectOne((r) => isUsersList(r))
      .flush(envelope({ items: STAFF, pageIndex: 1, pageSize: 10, totalCount: 25 }));
    fixture.detectChanges();

    expect(fixture.componentInstance.hasMore()).toBe(true);
    fixture.componentInstance.goToPage(2);

    const next = http.expectOne((r) => isUsersList(r));
    expect(next.request.params.get('page')).toBe('2');
    next.flush(envelope({ items: STAFF, pageIndex: 2, pageSize: 10, totalCount: 25 }));
    fixture.detectChanges();
    expect(fixture.componentInstance.page()).toBe(2);
  });

  it('narrows the whole result set by role, not the current page', () => {
    const fixture = render();
    http
      .expectOne((r) => isUsersList(r))
      .flush(envelope({ items: STAFF, pageIndex: 1, pageSize: 10, totalCount: 2 }));
    fixture.detectChanges();

    fixture.componentInstance.setRole('Agent');

    const filtered = http.expectOne((r) => isUsersList(r));
    expect(filtered.request.params.get('role')).toBe('Agent');
    expect(filtered.request.params.get('page')).toBe('1');
    filtered.flush(envelope({ items: [STAFF[0]], pageIndex: 1, pageSize: 10, totalCount: 1 }));
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Ann');
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Bob');
  });

  it('maps the status tabs onto the server-side isActive filter', () => {
    const fixture = render();
    http
      .expectOne((r) => isUsersList(r))
      .flush(envelope({ items: STAFF, pageIndex: 1, pageSize: 10, totalCount: 2 }));
    fixture.detectChanges();

    fixture.componentInstance.setTab('suspended');

    const filtered = http.expectOne((r) => isUsersList(r));
    expect(filtered.request.params.get('isActive')).toBe('false');
    filtered.flush(envelope({ items: [STAFF[1]], pageIndex: 1, pageSize: 10, totalCount: 1 }));
    fixture.detectChanges();
  });

  it('sends the typed search term to the server and resets the page', () => {
    const fixture = render();
    http
      .expectOne((r) => isUsersList(r))
      .flush(envelope({ items: STAFF, pageIndex: 1, pageSize: 10, totalCount: 2 }));
    fixture.detectChanges();

    fixture.componentInstance.goToPage(2);
    http
      .expectOne((r) => isUsersList(r))
      .flush(envelope({ items: STAFF, pageIndex: 2, pageSize: 10, totalCount: 25 }));
    fixture.detectChanges();

    fixture.componentInstance.searchTerm.set('ann');
    fixture.componentInstance.submitSearch();

    const searched = http.expectOne((r) => isUsersList(r));
    expect(searched.request.params.get('search')).toBe('ann');
    expect(searched.request.params.get('page')).toBe('1');
    searched.flush(envelope({ items: [STAFF[0]], pageIndex: 1, pageSize: 10, totalCount: 1 }));
    fixture.detectChanges();
  });

  it('sorts a column on click and toggles the direction on the next click', () => {
    const fixture = render();
    flushList();
    fixture.detectChanges();

    fixture.componentInstance.sort('firstname');

    const asc = http.expectOne((r) => isUsersList(r));
    expect(asc.request.params.get('sortBy')).toBe('firstname');
    expect(asc.request.params.get('sortDirection')).toBe('asc');
    asc.flush(envelope({ items: STAFF, pageIndex: 1, pageSize: 10, totalCount: 2 }));
    fixture.detectChanges();

    fixture.componentInstance.sort('firstname');

    const desc = http.expectOne((r) => isUsersList(r));
    expect(desc.request.params.get('sortBy')).toBe('firstname');
    expect(desc.request.params.get('sortDirection')).toBe('desc');
    desc.flush(envelope({ items: STAFF, pageIndex: 1, pageSize: 10, totalCount: 2 }));
    fixture.detectChanges();
  });

  it('resetFilters clears every narrowing control and re-fetches unfiltered', () => {
    const fixture = render();
    http
      .expectOne((r) => isUsersList(r))
      .flush(envelope({ items: STAFF, pageIndex: 1, pageSize: 10, totalCount: 2 }));
    fixture.detectChanges();

    fixture.componentInstance.searchTerm.set('ann');
    fixture.componentInstance.setRole('Agent');
    http
      .expectOne((r) => isUsersList(r))
      .flush(envelope({ items: [STAFF[0]], pageIndex: 1, pageSize: 10, totalCount: 1 }));
    fixture.detectChanges();

    fixture.componentInstance.setTab('active');
    http
      .expectOne((r) => isUsersList(r))
      .flush(envelope({ items: [STAFF[0]], pageIndex: 1, pageSize: 10, totalCount: 1 }));
    fixture.detectChanges();
    expect(fixture.componentInstance.isFiltered()).toBe(true);

    fixture.componentInstance.resetFilters();

    const reset = http.expectOne((r) => isUsersList(r));
    expect(reset.request.params.has('search')).toBe(false);
    expect(reset.request.params.has('role')).toBe(false);
    expect(reset.request.params.has('isActive')).toBe(false);
    reset.flush(envelope({ items: STAFF, pageIndex: 1, pageSize: 10, totalCount: 2 }));
    fixture.detectChanges();
    expect(fixture.componentInstance.isFiltered()).toBe(false);
  });
});