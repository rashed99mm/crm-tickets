import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { envelopeInterceptor } from 'common';
import { vi } from 'vitest';
import CustomerCreateComponent from './customer-create.component';

/** A field-keyed 400, exactly as the backend's ValidationBehavior emits it. */
const FIELD_ERROR_ENVELOPE = {
  success: false,
  code: 'VALIDATION_ERROR',
  message: 'Validation failed',
  data: null,
  errors: [{ field: 'name', code: 'VALIDATION_ERROR', message: 'Name must not exceed 200 characters' }],
};

/** A duplicate email is a 409 and names no field: the payload is well formed (AC-9). */
const DUPLICATE_EMAIL_ENVELOPE = {
  success: false,
  code: 'CUSTOMER_EMAIL_EXISTS',
  message: 'A customer with that email already exists',
  data: null,
  errors: [],
};

describe('CustomerCreateComponent', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        // The envelope interceptor is part of the contract under test: it is what turns the
        // backend's `error.details` dictionary into the field errors this form binds.
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  function render(): ComponentFixture<CustomerCreateComponent> {
    const fixture = TestBed.createComponent(CustomerCreateComponent);
    fixture.detectChanges();
    return fixture;
  }

  function fillValid(fixture: ComponentFixture<CustomerCreateComponent>) {
    fixture.componentInstance.form.setValue({
      name: 'Layla Haddad',
      email: 'layla@example.com',
      phone: '+20 100 555 0101',
    });
  }

  it('AC70: does not submit while the form is invalid', () => {
    const fixture = render();

    fixture.componentInstance.submit();

    http.expectNone('/api/Customers');
  });

  it('AC70: mirrors the server rules — a name over 200 characters is rejected before submitting', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.form.controls.name.setValue('x'.repeat(201));

    fixture.componentInstance.submit();

    expect(fixture.componentInstance.form.controls.name.hasError('maxlength')).toBe(true);
    http.expectNone('/api/Customers');
  });

  it('AC70: a malformed email is rejected before submitting', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.form.controls.email.setValue('not-an-email');

    fixture.componentInstance.submit();

    expect(fixture.componentInstance.form.controls.email.hasError('email')).toBe(true);
    http.expectNone('/api/Customers');
  });

  it('AC70: an omitted phone is sent as null, not as an empty string', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.form.controls.phone.setValue('');

    fixture.componentInstance.submit();

    const request = http.expectOne('/api/Customers');
    expect(request.request.body).toEqual({
      name: 'Layla Haddad',
      email: 'layla@example.com',
      phone: null,
    });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 'c-1' }, errors: [] });
  });

  /**
   * Asserts the message's POSITION, not merely its presence — a test that only checked the text
   * appeared somewhere would pass with every error dumped into a banner, which is what AC-70
   * forbids.
   */
  it('AC70: a server field error appears under the control it names', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.submit();

    http.expectOne('/api/Customers').flush(FIELD_ERROR_ENVELOPE, {
      status: 400,
      statusText: 'Bad Request',
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const nameInput = el.querySelector('input[aria-invalid="true"]') as HTMLElement | null;
    expect(nameInput).not.toBeNull();

    // The message must live in the element the input points at through aria-describedby.
    const describedBy = nameInput!.getAttribute('aria-describedby');
    expect(describedBy).toBeTruthy();
    expect(el.querySelector('#' + describedBy)?.textContent).toContain(
      'Name must not exceed 200 characters',
    );

    // And there must be no form-level copy of it.
    expect(fixture.componentInstance.formLevelError()).toBeNull();
  });

  /**
   * A duplicate email carries no field key because the payload is valid — it is the state of the
   * world that refuses it. Marking the email control invalid would tell the user the address they
   * typed is malformed, which it is not.
   */
  it('AC70: a duplicate email renders at form level, not on a field', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.submit();

    http.expectOne('/api/Customers').flush(DUPLICATE_EMAIL_ENVELOPE, {
      status: 409,
      statusText: 'Conflict',
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.formLevelError()).not.toBeNull();
    expect(fixture.componentInstance.fieldError('email')).toBeNull();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('A customer with that email already exists');
    expect(el.querySelector('input[aria-invalid="true"]')).toBeNull();
  });

  it('AC70: a created customer lands on their detail screen', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.submit();

    http.expectOne('/api/Customers').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { id: 'c-1' },
      errors: [],
    });
    fixture.detectChanges();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/customers/c-1');
  });

  it('AC70: does not submit twice while a request is in flight', () => {
    const fixture = render();
    fillValid(fixture);

    fixture.componentInstance.submit();
    fixture.componentInstance.submit();

    // expectOne fails if a second request was issued.
    http.expectOne('/api/Customers').flush({ success: true, code: 'CON035', message: 'OK', data: { id: 'c-1' }, errors: [] });
  });

  /** A corrected field still showing the old rejection makes the form look broken. */
  it('clears a server field error once the user edits that control', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.submit();

    http.expectOne('/api/Customers').flush(FIELD_ERROR_ENVELOPE, {
      status: 400,
      statusText: 'Bad Request',
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.fieldError('name')).not.toBeNull();

    fixture.componentInstance.clearServerError('name');

    expect(fixture.componentInstance.fieldError('name')).toBeNull();
  });
});
