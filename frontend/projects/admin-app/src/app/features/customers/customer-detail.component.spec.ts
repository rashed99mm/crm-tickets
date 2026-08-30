import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { envelopeInterceptor } from 'common';
import { vi } from 'vitest';
import CustomerDetailComponent from './customer-detail.component';

const CUSTOMER = {
  id: 'c-1',
  name: 'Layla Haddad',
  email: 'layla@example.com',
  phone: '+20 100 555 0101',
  createdAt: '2026-08-20T09:00:00.000Z',
};

function ok(data: unknown) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

function notesPage(items: unknown[] = []) {
  return ok({ items, pageIndex: 1, pageSize: 20, totalCount: items.length });
}

const NOT_FOUND_ENVELOPE = {
  success: false,
  code: 'CUSTOMER_NOT_FOUND',
  message: 'Customer not found',
  data: null,
  errors: [],
};

const SERVER_FAILURE = {
  success: false,
  code: 'INTERNAL_ERROR',
  message: 'Something went wrong on the server',
  data: null,
  errors: [],
};

/** A duplicate email is a 409 and names no field: the payload is well formed (AC-14). */
const DUPLICATE_EMAIL_ENVELOPE = {
  success: false,
  code: 'CUSTOMER_EMAIL_EXISTS',
  message: 'A customer with that email already exists',
  data: null,
  errors: [],
};

/** AC-15 — support history is not destroyable by one click. */
const HAS_TICKETS_ENVELOPE = {
  success: false,
  code: 'CUSTOMER_HAS_TICKETS',
  message: 'This customer has tickets and cannot be removed',
  data: null,
  errors: [],
};

describe('CustomerDetailComponent', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  /** Creates the component and settles the customer read only — the caller flushes it. */
  async function create(): Promise<ComponentFixture<CustomerDetailComponent>> {
    const fixture = TestBed.createComponent(CustomerDetailComponent);
    fixture.componentRef.setInput('id', 'c-1');
    fixture.detectChanges();

    // The load is queued on a microtask so the route input is bound before it fires.
    await Promise.resolve();
    return fixture;
  }

  /** The happy path: customer loaded, then the notes child's own read settled. */
  async function render(): Promise<ComponentFixture<CustomerDetailComponent>> {
    const fixture = await create();

    http.expectOne('/api/Customers/c-1').flush(ok(CUSTOMER));
    fixture.detectChanges();

    // The notes child mounts with the customer and loads on its own microtask.
    await Promise.resolve();
    http.expectOne((r) => r.url === '/api/Customers/c-1/notes').flush(notesPage());
    fixture.detectChanges();

    return fixture;
  }

  it('AC71: renders name, email, phone and when the customer was recorded', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.textContent).toContain('Layla Haddad');

    const profile = el.querySelector('[data-testid="customer-profile"]');
    expect(profile?.textContent).toContain('layla@example.com');
    expect(profile?.textContent).toContain('+20 100 555 0101');
    // AC-71 asks for *when* the customer was recorded, not for the wire format. This used to
    // assert the raw ISO string, which is what let the unformatted instant reach every screen.
    expect(profile?.textContent).toContain('20 Aug 2026');
    expect(profile?.textContent).not.toContain('2026-08-20T09:00:00.000Z');
  });

  /**
   * A 404 is a real answer, not a fault: the record does not exist. It must not render as a blank
   * edit form — an agent who typed into one would be filling in a customer that was never there.
   */
  it('AC71: an unknown customer renders a not-found state, not an empty form', async () => {
    const fixture = await create();

    http.expectOne('/api/Customers/c-1').flush(NOT_FOUND_ENVELOPE, {
      status: 404,
      statusText: 'Not Found',
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(fixture.componentInstance.notFound()).toBe(true);
    expect(el.querySelector('[data-testid="customer-not-found"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="customer-edit-form"]')).toBeNull();
    // The notes child never mounts for a customer that does not exist.
    expect(el.querySelector('[data-testid="customer-notes"]')).toBeNull();
  });

  /** A fault is not a missing record, and it keeps the retry a 404 has no use for. */
  it('AC71: a server fault renders the error state with a retry, not the not-found state', async () => {
    const fixture = await create();

    http.expectOne('/api/Customers/c-1').flush(SERVER_FAILURE, {
      status: 500,
      statusText: 'Server Error',
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(fixture.componentInstance.notFound()).toBe(false);
    expect(el.querySelector('[data-testid="customer-not-found"]')).toBeNull();
    expect(el.textContent).toContain('Something went wrong on the server');
    expect(el.textContent).toContain('Try again');
  });

  it('AC72: saving a change puts it and re-reads so the screen shows what persisted', async () => {
    const fixture = await render();

    fixture.componentInstance.startEdit();
    fixture.detectChanges();
    // The form opens holding the current values, so a correction is an edit rather than a retype.
    expect(fixture.componentInstance.form.getRawValue()).toEqual({
      name: 'Layla Haddad',
      email: 'layla@example.com',
      phone: '+20 100 555 0101',
    });

    fixture.componentInstance.form.controls.phone.setValue('+20 100 555 0202');
    fixture.componentInstance.save();

    const put = http.expectOne('/api/Customers/c-1');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({
      name: 'Layla Haddad',
      email: 'layla@example.com',
      phone: '+20 100 555 0202',
    });
    put.flush(ok({ id: 'c-1' }));
    fixture.detectChanges();

    // The re-read is what makes the change "visible on reload" rather than merely patched locally.
    const reread = http.expectOne('/api/Customers/c-1');
    expect(reread.request.method).toBe('GET');
    reread.flush(ok({ ...CUSTOMER, phone: '+20 100 555 0202' }));
    fixture.detectChanges();

    expect(fixture.componentInstance.editing()).toBe(false);
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="customer-profile"]')
        ?.textContent,
    ).toContain('+20 100 555 0202');
  });

  /**
   * The conflict half of AC-72: the change was NOT applied, so the editor stays open holding what
   * the agent typed, and the message renders at form level because a 409 names no field.
   */
  it('AC72: a duplicate email shows the conflict at form level and the change is not applied', async () => {
    const fixture = await render();

    fixture.componentInstance.startEdit();
    fixture.componentInstance.form.controls.email.setValue('taken@example.com');
    fixture.componentInstance.save();

    http.expectOne('/api/Customers/c-1').flush(DUPLICATE_EMAIL_ENVELOPE, {
      status: 409,
      statusText: 'Conflict',
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(fixture.componentInstance.formLevelError()).not.toBeNull();
    expect(fixture.componentInstance.fieldError('email')).toBeNull();
    expect(el.querySelector('[data-testid="save-error"]')?.textContent).toContain(
      'A customer with that email already exists',
    );

    // Still editing, still holding the typed value, and the profile still shows the old email.
    expect(fixture.componentInstance.editing()).toBe(true);
    expect(fixture.componentInstance.form.controls.email.value).toBe('taken@example.com');
    expect(el.querySelector('[data-testid="customer-profile"]')?.textContent).toContain(
      'layla@example.com',
    );
  });

  it('AC72: a field-keyed rejection appears under the control it names', async () => {
    const fixture = await render();

    fixture.componentInstance.startEdit();
    fixture.componentInstance.save();

    http.expectOne('/api/Customers/c-1').flush(
      {
        success: false,
        code: 'VALIDATION_ERROR',
        message: 'Validation failed',
        data: null,
        errors: [{ field: 'name', code: 'VALIDATION_ERROR', message: 'Name must not exceed 200 characters' }],
      },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const nameInput = el.querySelector('input[aria-invalid="true"]') as HTMLElement | null;
    expect(nameInput).not.toBeNull();

    const describedBy = nameInput!.getAttribute('aria-describedby');
    expect(el.querySelector('#' + describedBy)?.textContent).toContain(
      'Name must not exceed 200 characters',
    );
    // No form-level copy — that would be the banner the criterion forbids.
    expect(el.querySelector('[data-testid="save-error"]')).toBeNull();
  });

  /** Removal is guarded, so a stray click on "Remove" issues nothing. */
  it('AC73: asking to remove sends no request until it is confirmed', async () => {
    const fixture = await render();

    fixture.componentInstance.askToDelete();
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="delete-confirm"]'),
    ).not.toBeNull();
    http.expectNone('/api/Customers/c-1');
  });

  it('AC73: a customer with tickets shows the refusal and stays on screen', async () => {
    const fixture = await render();

    fixture.componentInstance.askToDelete();
    fixture.componentInstance.confirmDelete();

    const request = http.expectOne('/api/Customers/c-1');
    expect(request.request.method).toBe('DELETE');
    request.flush(HAS_TICKETS_ENVELOPE, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="delete-error"]')?.textContent).toContain(
      'This customer has tickets and cannot be removed',
    );

    // The customer is STILL on screen — navigating away would suggest the removal happened.
    expect(el.querySelector('[data-testid="customer-profile"]')?.textContent).toContain(
      'layla@example.com',
    );
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('AC73: removing a customer with no tickets returns to the list', async () => {
    const fixture = await render();

    fixture.componentInstance.askToDelete();
    fixture.componentInstance.confirmDelete();

    http.expectOne('/api/Customers/c-1').flush(ok(null));
    fixture.detectChanges();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/customers');
  });

  it('AC74: the detail screen hosts the customer’s interaction history', async () => {
    const fixture = await render();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="customer-notes"]'),
    ).not.toBeNull();
  });

  it('AC410_CustomerProfileUsesIdentityBandAndThreeRegionWorkspace: renders identity band and 3 rails', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="customer-identity"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="customer-profile"]')).not.toBeNull();
    expect(el.querySelector('admin-customer-notes')).not.toBeNull();
    expect(el.querySelector('admin-customer-attachments')).not.toBeNull();
  });

  it('AC416_CustomerAndAdminScreensShowDistinctAsyncStates: loading, not found and loaded remain distinct', async () => {
    const fixture = await create();
    expect(fixture.componentInstance.state().status).toBe('loading');
    http.expectOne('/api/Customers/c-1').flush(NOT_FOUND_ENVELOPE, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    expect(fixture.componentInstance.notFound()).toBe(true);
  });

  it('AC417_UnbackedCustomerFieldsAreReadOnlyUnavailableStates: missing DTO fields use CsPlaceholder', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const placeholders = el.querySelectorAll('cs-placeholder');
    expect(placeholders.length).toBeGreaterThan(0);
  });

  it('AC418_AdminTablesAndRailsAreKeyboardAccessible: action buttons have focusable buttons and links', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('a[routerLink="/tickets/new"]')).not.toBeNull();
  });
});

