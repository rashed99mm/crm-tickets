import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from 'common';
import WebFormComponent from './web-form.component';

function envelope(data: unknown) {
  return { success: true, code: 'OK', message: 'OK', data, errors: [] };
}

describe('WebFormComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function render(): ComponentFixture<WebFormComponent> {
    const fixture = TestBed.createComponent(WebFormComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('submits valid form and displays ticket reference', () => {
    const fixture = render();
    fixture.componentInstance.form.setValue({
      name: 'Diana Prince',
      email: 'diana@themyscira.gov',
      subject: 'Diplomatic Inquiry',
      description: 'Requesting assistance with an artifact.',
      website: '',
    });

    fixture.componentInstance.submit();

    const req = http.expectOne('/api/external/webform/submit');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.name).toBe('Diana Prince');
    // The real generator issues TKT-nnnnnn (TicketReferenceGenerator.cs:49).
    req.flush(envelope({ reference: 'TKT-000123', success: true }));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('TKT-000123');
    expect(fixture.componentInstance.submittedReference()).toBe('TKT-000123');
  });

  it('sends the honeypot value to the backend instead of deciding locally (CC-22/CC-47)', () => {
    // The honeypot defence belongs to the server: a bot posts directly and never runs this code,
    // and deciding here silently discarded a real submission whenever a browser autofilled the
    // hidden input. The component now always posts and shows whatever reference comes back.
    const fixture = render();
    fixture.componentInstance.form.setValue({
      name: 'Spam Bot',
      email: 'bot@spam.com',
      subject: 'Buy crypto',
      description: 'Free coins',
      website: 'http://spam.ru',
    });

    fixture.componentInstance.submit();

    const req = http.expectOne('/api/external/webform/submit');
    expect(req.request.body.honeypot).toBe('http://spam.ru');

    // The backend answers a bot with a response indistinguishable from a real one (CC-47), so the
    // component has nothing special to render.
    req.flush(envelope({ reference: 'TKT-000999', success: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.submittedReference()).toBe('TKT-000999');
  });

  it('omits the honeypot field when the hidden input is untouched', () => {
    const fixture = render();
    fixture.componentInstance.form.setValue({
      name: 'Real Person',
      email: 'real@example.com',
      subject: 'Cannot sign in',
      description: 'The page rejects my password.',
      website: '',
    });

    fixture.componentInstance.submit();

    const req = http.expectOne('/api/external/webform/submit');
    expect(req.request.body.honeypot).toBeUndefined();

    req.flush(envelope({ reference: 'TKT-000124', success: true }));
  });
});
