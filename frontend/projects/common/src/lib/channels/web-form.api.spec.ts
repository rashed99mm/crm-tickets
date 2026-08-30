import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { WebFormApi, WebFormSubmissionRequest, WebFormSubmissionResponse } from './web-form.api';

describe('WebFormApi', () => {
  let api: WebFormApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(WebFormApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('submit: POSTs request to /api/external/webform/submit', () => {
    const payload: WebFormSubmissionRequest = {
      name: 'John Doe',
      email: 'john@example.com',
      subject: 'Inquiry',
      description: 'Need assistance',
    };

    let received: WebFormSubmissionResponse | undefined;
    api.submit(payload).subscribe((res) => (received = res));

    const req = http.expectOne('/api/external/webform/submit');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);

    req.flush({ reference: 'TICK-999', success: true });
    expect(received?.reference).toBe('TICK-999');
  });
});
