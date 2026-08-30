import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import CustomerListComponent from './customer-list.component';

function page(items: unknown[], totalCount = items.length) {
  return {
    success: true,
    code: 'CON035',
    message: 'OK',
    data: { items, pageIndex: 1, pageSize: 10, totalCount },
    errors: [],
  };
}

const CUSTOMER = {
  id: 'c-1',
  name: 'Layla Haddad',
  email: 'layla@example.com',
  phone: '+20 100 555 0101',
  createdAt: '2026-08-20T09:00:00Z',
};

const SERVER_FAILURE = {
  success: false,
  code: 'INTERNAL_ERROR',
  message: 'Something went wrong on the server',
  data: null,
  errors: [],
};

describe('CustomerListComponent', () => {
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

  function render(): ComponentFixture<CustomerListComponent> {
    const fixture = TestBed.createComponent(CustomerListComponent);
    fixture.detectChanges();
    return fixture;
  }

  function flushList(
    fixture: ComponentFixture<CustomerListComponent>,
    body: object,
    status?: number,
  ) {
    const request = http.expectOne((r) => r.url === '/api/Customers');
    if (status) {
      request.flush(body, { status, statusText: 'Error' });
    } else {
      request.flush(body);
    }
    fixture.detectChanges();
    return request;
  }

  it('AC69: lists customers with name, email and phone', () => {
    const fixture = render();
    flushList(fixture, page([CUSTOMER]));

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Layla Haddad');
    expect(text).toContain('layla@example.com');
    expect(text).toContain('+20 100 555 0101');
  });

  it('AC69: searching refetches with the search term, from the first page', () => {
    const fixture = render();
    flushList(fixture, page([CUSTOMER], 25));
    fixture.componentInstance.goToPage(2);
    flushList(fixture, page([CUSTOMER], 25));

    fixture.componentInstance.applySearch('layla');

    const request = http.expectOne((r) => r.url === '/api/Customers');
    expect(request.request.params.get('search')).toBe('layla');
    // A new search is a new result set; staying on page 2 would show an empty page of it.
    expect(request.request.params.get('page')).toBe('1');
    request.flush(page([CUSTOMER]));
  });

  it('AC69: shows a loading state while the request is in flight', () => {
    const fixture = render();

    expect(fixture.componentInstance.state().status).toBe('loading');
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="status"]')).not.toBeNull();

    flushList(fixture, page([CUSTOMER]));
  });

  /**
   * The half of AC-69 that matters. `catchError(() => of([]))` is the default mistake here and it
   * turns a 500 into "no customers": the user sees an empty database, creates a duplicate record,
   * and the outage is never reported.
   */
  it('AC69: a failed load renders the error state, not an empty list', () => {
    const fixture = render();
    flushList(fixture, SERVER_FAILURE, 500);

    expect(fixture.componentInstance.state().status).toBe('error');

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')?.textContent).toContain(
      'Something went wrong on the server',
    );
    expect(el.textContent).not.toContain('No customers recorded yet');
  });

  /**
   * The other half. The retry button's presence in the error state and ABSENCE here is both the
   * honest signal — nothing failed, so there is nothing to retry — and the visual difference that
   * stops the two states reading alike.
   */
  it('AC69: a successful empty result renders the empty state, with no retry offered', () => {
    const fixture = render();
    flushList(fixture, page([]));

    expect(fixture.componentInstance.state().status).toBe('empty');

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('No customers recorded yet');
    expect(el.querySelector('button')).toBeNull();
  });

  /** Telling someone their records are gone when their search simply matched nothing is a lie. */
  it('AC69: an empty search says the search matched nothing, not that there are no customers', () => {
    const fixture = render();
    flushList(fixture, page([CUSTOMER]));

    fixture.componentInstance.applySearch('nobody');
    flushList(fixture, page([]));

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('No customers match that search');
    expect(text).not.toContain('No customers recorded yet');
  });

  it('AC69: advances the page parameter when the next page is requested', () => {
    const fixture = render();
    // 25 total over a page size of 10, so a next page exists.
    flushList(fixture, page([CUSTOMER], 25));

    fixture.componentInstance.goToPage(2);

    const request = http.expectOne((r) => r.url === '/api/Customers');
    expect(request.request.params.get('page')).toBe('2');
    request.flush(page([CUSTOMER], 25));
  });

  it('AC69: retrying after a failure re-issues the request', () => {
    const fixture = render();
    flushList(fixture, SERVER_FAILURE, 500);

    fixture.componentInstance.load();

    expect(fixture.componentInstance.state().status).toBe('loading');
    flushList(fixture, page([CUSTOMER]));
    expect(fixture.componentInstance.state().status).toBe('loaded');
  });
});
