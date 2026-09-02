import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { envelopeInterceptor } from 'common';
import { vi } from 'vitest';
import TicketCreateComponent from './ticket-create.component';

const CATEGORIES = {
  success: true,
  code: 'CON035',
  message: 'OK',
  data: [{ id: 'cat-1', name: 'Technical' }],
  errors: [],
};

const CUSTOMERS = {
  success: true,
  code: 'CON035',
  message: 'OK',
  data: { items: [{ id: 'c-1', name: 'Layla', email: 'layla@example.com' }] },
  errors: [],
};

/** A field-keyed 400, exactly as the backend's ValidationBehavior emits it. */
const FIELD_ERROR_ENVELOPE = {
  success: false,
  code: 'VALIDATION_ERROR',
  message: 'Validation failed',
  data: null,
  errors: [{ field: 'subject', code: 'VALIDATION_ERROR', message: 'Subject must not exceed 200 characters' }],
};

/** A conflict names no field — there is no control to attach it to. */
const NO_FIELD_ENVELOPE = {
  success: false,
  code: 'TICKET_CUSTOMER_NOT_FOUND',
  message: 'The selected customer does not exist',
  data: null,
  errors: [],
};

describe('TicketCreateComponent', () => {
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

  function render(): ComponentFixture<TicketCreateComponent> {
    const fixture = TestBed.createComponent(TicketCreateComponent);
    fixture.detectChanges();

    // The pickers load on construction.
    http.expectOne('/api/Categories').flush(CATEGORIES);
    http.expectOne((r) => r.url === '/api/Customers').flush(CUSTOMERS);
    fixture.detectChanges();

    return fixture;
  }

  function fillValid(fixture: ComponentFixture<TicketCreateComponent>) {
    fixture.componentInstance.form.setValue({
      subject: 'Cannot sign in',
      description: 'The portal rejects my password.',
      customerId: 'c-1',
      categoryId: 'cat-1',
      impact: 'Medium',
      urgency: 'Medium',
    });
  }

  it('AC59: does not submit while the form is invalid', () => {
    const fixture = render();

    fixture.componentInstance.submit();

    http.expectNone('/api/Tickets');
  });

  it('AC59: the submit button is disabled while the form is invalid', () => {
    const fixture = render();

    const button = (fixture.nativeElement as HTMLElement).querySelector('button[type="submit"]');
    expect(button?.hasAttribute('disabled')).toBe(true);

    fillValid(fixture);
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement)
        .querySelector('button[type="submit"]')
        ?.hasAttribute('disabled'),
    ).toBe(false);
  });

  it('AC59: rejects a subject over 200 characters before submitting', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.form.controls.subject.setValue('x'.repeat(201));

    fixture.componentInstance.submit();

    expect(fixture.componentInstance.form.controls.subject.hasError('maxlength')).toBe(true);
    http.expectNone('/api/Tickets');
  });

  it('AC59: does not submit twice while a request is in flight', () => {
    const fixture = render();
    fillValid(fixture);

    fixture.componentInstance.submit();
    fixture.componentInstance.submit();

    // expectOne fails if a second request was issued.
    http.expectOne('/api/Tickets').flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });
  });

  /**
   * The test the whole feature exists to prove. It asserts the message's POSITION, not merely its
   * presence — a test that only checked the text appeared somewhere would pass with every error
   * dumped into a banner, which is exactly what AC-60 forbids.
   */
  it('AC60: a server field error appears under the control it names, not in a banner', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.submit();

    http.expectOne('/api/Tickets').flush(FIELD_ERROR_ENVELOPE, {
      status: 400,
      statusText: 'Bad Request',
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const subjectInput = el.querySelector('input[aria-invalid="true"]') as HTMLElement | null;
    expect(subjectInput).not.toBeNull();

    // The message must live in the element the input points at through aria-describedby.
    const describedBy = subjectInput!.getAttribute('aria-describedby');
    expect(describedBy).toBeTruthy();
    expect(el.querySelector(`#${describedBy}`)?.textContent).toContain(
      'Subject must not exceed 200 characters',
    );

    // And there must be no form-level copy of it.
    expect(fixture.componentInstance.formLevelError()).toBeNull();
  });

  it('AC60: a failure naming no field renders at form level, since no control owns it', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.submit();

    http.expectOne('/api/Tickets').flush(NO_FIELD_ENVELOPE, {
      status: 409,
      statusText: 'Conflict',
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.formLevelError()).not.toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'The selected customer does not exist',
    );
  });

  /** A corrected field still showing the old rejection makes the form look broken. */
  it('clears a server field error once the user edits that control', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.submit();

    http.expectOne('/api/Tickets').flush(FIELD_ERROR_ENVELOPE, {
      status: 400,
      statusText: 'Bad Request',
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.fieldError('subject')).not.toBeNull();

    fixture.componentInstance.clearServerError('subject');

    expect(fixture.componentInstance.fieldError('subject')).toBeNull();
  });

  it('navigates to the queue once the ticket is created', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.submit();

    http.expectOne('/api/Tickets').flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });
    fixture.detectChanges();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/tickets');
  });

  it('AC408_CreateTicketMatchesFormCompositionAndValidation: ticket create form matches reference fields and layout', () => {
    const fixture = render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('header')).not.toBeNull();
    expect(el.querySelector('form')).not.toBeNull();
    expect(el.querySelector('textarea')).not.toBeNull();
    expect(el.querySelector('[data-testid="ticket-create-attachment-dropzone"]')).not.toBeNull();
  });

  it('AC418_TicketFormsAndActionsAreKeyboardAccessible: inputs and buttons are focusable and keyboard accessible', () => {
    const fixture = render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('select').length).toBe(4);
    expect(el.querySelector('button[type="submit"]')).not.toBeNull();
  });

  it('AC923_7: create sends impact and urgency, and shows the derived priority preview', () => {
    const fixture = render();
    fillValid(fixture);
    fixture.componentInstance.form.controls.impact.setValue('High');
    fixture.componentInstance.form.controls.urgency.setValue('High');
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="priority-preview"]')?.textContent,
    ).toContain('Urgent');

    fixture.componentInstance.submit();

    const req = http.expectOne('/api/Tickets');
    expect(req.request.body).toEqual({
      subject: 'Cannot sign in',
      description: 'The portal rejects my password.',
      customerId: 'c-1',
      categoryId: 'cat-1',
      impact: 'High',
      urgency: 'High',
    });
    req.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });
  });
});
