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
    req.flush(envelope({ reference: 'TICK-AMZ-100', success: true }));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('TICK-AMZ-100');
    expect(fixture.componentInstance.submittedReference()).toBe('TICK-AMZ-100');
  });

  it('fakes success for bot when honeypot is filled without hitting API', () => {
    const fixture = render();
    fixture.componentInstance.form.setValue({
      name: 'Spam Bot',
      email: 'bot@spam.com',
      subject: 'Buy crypto',
      description: 'Free coins',
      website: 'http://spam.ru',
    });

    fixture.componentInstance.submit();
    http.expectNone('/api/external/webform/submit');
    fixture.detectChanges();

    expect(fixture.componentInstance.submittedReference()).not.toBeNull();
  });
});
