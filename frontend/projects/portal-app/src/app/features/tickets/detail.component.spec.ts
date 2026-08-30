import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import PortalTicketDetailComponent from './detail.component';

const DETAIL = {
  id: 't1',
  reference: 'T-0001',
  subject: 'Printer on fire',
  description: 'It is very warm.',
  status: 'Resolved',
  priority: 'High',
  createdAt: '2026-08-27T00:00:00Z',
  messages: [{ direction: 'Outbound', body: 'We fixed it.', sentAt: '2026-08-27T01:00:00Z' }],
  surveySubmitted: false,
};

describe('PortalTicketDetailComponent', () => {
  let http: HttpTestingController;

  function setup() {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [PortalTicketDetailComponent],
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
    const fixture = TestBed.createComponent(PortalTicketDetailComponent);
    fixture.componentRef.setInput('id', 't1');
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => http.verify());

  it('loads the ticket from the portal surface (US-406) and shows the message timeline (US-413)', () => {
    const fixture = create();
    const req = http.expectOne((r) => r.url === '/api/portal/tickets/t1');
    expect(req.request.method).toBe('GET');
    req.flush(DETAIL);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Printer on fire');
    expect(el.textContent).toContain('We fixed it.');
  });

  it('shows the survey on a resolved ticket and posts the rating (US-408/409/415)', () => {
    const fixture = create();
    http.expectOne((r) => r.url === '/api/portal/tickets/t1').flush(DETAIL);
    fixture.detectChanges();

    fixture.componentInstance.setRating(5);
    fixture.componentInstance.comment.set('Great!');
    fixture.componentInstance.submitSurvey();

    const survey = http.expectOne((r) => r.url === '/api/portal/tickets/t1/survey');
    expect(survey.request.method).toBe('POST');
    expect(survey.request.body).toEqual({ rating: 5, comment: 'Great!' });
    survey.flush({ id: 's1' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Thanks');
  });

  it('sends a reply through the portal surface (US-407/414)', () => {
    const fixture = create();
    const get = http.expectOne((r) => r.url === '/api/portal/tickets/t1');
    get.flush({ ...DETAIL, status: 'Open' });
    fixture.detectChanges();

    fixture.componentInstance.replyBody.set('It caught fire again.');
    fixture.componentInstance.sendReply();

    const reply = http.expectOne((r) => r.url === '/api/portal/tickets/t1/reply');
    expect(reply.request.method).toBe('POST');
    expect(reply.request.body).toEqual({ body: 'It caught fire again.' });
    reply.flush({ id: 'm2' });
    http.expectOne((r) => r.url === '/api/portal/tickets/t1').flush(DETAIL);
    fixture.detectChanges();
  });
});
