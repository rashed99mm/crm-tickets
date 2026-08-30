import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AiApi, envelopeInterceptor } from 'common';
import { AiPanelComponent } from './ai-panel.component';

describe('AiPanelComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [AiPanelComponent],
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
    const fixture = TestBed.createComponent(AiPanelComponent);
    fixture.componentRef.setInput('ticketId', 't1');
    fixture.detectChanges();
    return fixture;
  }

  it('starts every card idle with its generation button enabled, not preloading', () => {
    const fixture = create();

    expect(fixture.componentInstance.summary().status).toBe('idle');
    expect(fixture.componentInstance.replies().status).toBe('idle');
    expect(fixture.componentInstance.solutions().status).toBe('idle');
    expect(fixture.componentInstance.categories().status).toBe('idle');

    const el = fixture.nativeElement as HTMLElement;
    const buttons = Array.from(el.querySelectorAll('cs-button')) as HTMLElement[];
    const disabled = buttons.filter((b) => b.getAttribute('ng-reflect-disabled') === 'true');
    expect(disabled.length).toBe(0);
  });

  it('shows the draft with Accept/Reject while pending', () => {
    const fixture = create();
    fixture.componentInstance.summarise();

    const req = http.expectOne('/api/Tickets/t1/ai/summary');
    req.flush({ id: 's1', kind: 'Summary', payload: { text: 'The customer is locked out.' }, status: 'Pending', edited: false });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('The customer is locked out.');
    expect(el.textContent).toContain('Accept');
    expect(el.textContent).toContain('Reject');
  });

  it('accepting clears the pending block and offers no second resolve', () => {
    const fixture = create();
    fixture.componentInstance.summarise();
    http.expectOne('/api/Tickets/t1/ai/summary')
      .flush({ id: 's1', kind: 'Summary', payload: { text: 'x' }, status: 'Pending', edited: false });
    fixture.detectChanges();

    fixture.componentInstance.resolve('summary', 'accept');

    const req = http.expectOne('/api/Tickets/t1/ai/suggestions/s1');
    expect(req.request.body).toEqual({ action: 'accept', editedPayload: undefined });
    req.flush({ id: 's1', kind: 'Summary', payload: {}, status: 'Accepted', edited: false });
    fixture.detectChanges();

    expect(fixture.componentInstance.summary().status).toBe('loading');
  });

  it('routes suggested articles to the admin KB surface instead of a missing detail route', () => {
    const fixture = create();
    fixture.componentInstance.suggestSolutions();

    http.expectOne('/api/Tickets/t1/ai/solutions').flush({
      id: 's2',
      kind: 'Solutions',
      payload: { articles: [{ id: 'a1', title: 'Reset a password' }] },
      status: 'Pending',
      edited: false,
    });
    fixture.detectChanges();

    const link = (fixture.nativeElement as HTMLElement).querySelector('a');
    expect(link?.getAttribute('href')).toBe('/kb-admin');
  });

  it('hides itself entirely after the first ERR052 degraded answer', () => {
    const fixture = create();
    fixture.componentInstance.suggestCategories();

    http.expectOne('/api/Tickets/t1/ai/categories').flush(
      {
        success: false,
        code: 'ERR052',
        message: 'AI assist is not enabled in this deployment.',
        data: null,
        errors: [],
        traceId: 'tr',
        timestamp: '2026-08-27T00:00:00Z',
      },
      { status: 503, statusText: 'Service Unavailable' },
    );
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="ai-rail"]')).toBeNull();
    expect(fixture.componentInstance.available()).toBeFalsy();
  });
});
