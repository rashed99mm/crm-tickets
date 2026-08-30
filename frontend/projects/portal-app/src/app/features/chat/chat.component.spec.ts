import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import { PortalChatComponent } from './chat.component';

describe('PortalChatComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [PortalChatComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function create() {
    const fixture = TestBed.createComponent(PortalChatComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('sends the question and renders the grounded answer with citation links', () => {
    const fixture = create();
    fixture.componentInstance.question.set('How do I reset my password?');
    fixture.componentInstance.send();

    const req = http.expectOne('/api/knowledge-base/ask');
    expect(req.request.body).toEqual({ question: 'How do I reset my password?' });
    req.flush({
      answer: 'Open settings and choose reset.',
      citations: [{ articleId: 'a1', title: 'Reset Password' }],
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Open settings and choose reset.');
    // The citation must be a KB detail link; asserting the element and its label rather than the
    // reflected attribute, which routerLink only sets once an outlet-backed navigation exists.
    const citation = Array.from(el.querySelectorAll('a')).find((a) =>
      a.textContent?.includes('Reset Password'),
    );
    expect(citation?.textContent?.trim()).toBe('Reset Password');
  });

  it('renders the ERR053 refusal as dictionary copy, not an exception message', () => {
    const fixture = create();
    fixture.componentInstance.question.set('What is the meaning of life?');
    fixture.componentInstance.send();

    http.expectOne('/api/knowledge-base/ask').flush(
      {
        success: false,
        code: 'ERR053',
        message: 'ungrounded',
        data: null,
        errors: [],
        traceId: 'tr',
        timestamp: '2026-08-27T00:00:00Z',
      },
      { status: 404, statusText: 'Not Found' },
    );
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('raise a ticket');
    expect(el.textContent).not.toContain('ungrounded');
    expect(el.querySelector('[data-refusal="true"]')).not.toBeNull();
  });

  it('does nothing on an empty question', () => {
    const fixture = create();
    fixture.componentInstance.question.set('   ');
    fixture.componentInstance.send();

    http.verify();
    expect(fixture.componentInstance.messages().length).toBe(0);
  });
});
