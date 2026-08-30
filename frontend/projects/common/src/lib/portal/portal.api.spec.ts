import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from '../api/envelope.interceptor';
import { PortalApi } from './portal.api';

describe('PortalApi', () => {
  let api: PortalApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    api = TestBed.inject(PortalApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists the customer own tickets from /api/portal/tickets', () => {
    api.listTickets().subscribe();
    const request = http.expectOne('/api/portal/tickets');
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: [], errors: [] });
  });

  it('fetches one ticket from /api/portal/tickets/:id', () => {
    api.getTicket('t-1').subscribe();
    const request = http.expectOne('/api/portal/tickets/t-1');
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });
  });

  it('submits a ticket with no customerId in the body', () => {
    api
      .submitTicket({
        subject: 'Cannot sign in',
        description: 'The portal rejects my password.',
        categoryId: 'cat-1',
        priority: 'Normal',
      })
      .subscribe();

    const request = http.expectOne('/api/portal/tickets');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      subject: 'Cannot sign in',
      description: 'The portal rejects my password.',
      categoryId: 'cat-1',
      priority: 'Normal',
    });
    expect(request.request.body).not.toHaveProperty('customerId');
    request.flush({ success: true, code: 'CON032', message: 'OK', data: { id: 't-1' }, errors: [] });
  });

  it('replies to a ticket at /api/portal/tickets/:id/reply', () => {
    api.reply('t-1', 'Any update?').subscribe();
    const request = http.expectOne('/api/portal/tickets/t-1/reply');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ body: 'Any update?' });
    request.flush({ success: true, code: 'CON032', message: 'OK', data: { id: 'm-1' }, errors: [] });
  });

  it('submits a survey at /api/portal/tickets/:id/survey', () => {
    api.submitSurvey('t-1', { rating: 5, comment: 'great' }).subscribe();
    const request = http.expectOne('/api/portal/tickets/t-1/survey');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ rating: 5, comment: 'great' });
    request.flush({ success: true, code: 'CON069', message: 'OK', data: { id: 's-1' }, errors: [] });
  });

  it('reads categories from /api/Categories', () => {
    api.listCategories().subscribe();
    const request = http.expectOne('/api/Categories');
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: [], errors: [] });
  });
});
