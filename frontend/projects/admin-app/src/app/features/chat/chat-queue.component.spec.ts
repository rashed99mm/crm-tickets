import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { envelopeInterceptor } from 'common';
import ChatQueueComponent from './chat-queue.component';

function envelope(data: unknown) {
  return { success: true, code: 'OK', message: 'OK', data, errors: [] };
}

function paged(items: readonly unknown[]) {
  return { items, pageIndex: 1, pageSize: 10, totalCount: items.length };
}

describe('ChatQueueComponent', () => {
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
  });

  afterEach(() => http.verify());

  function render(): ComponentFixture<ChatQueueComponent> {
    const fixture = TestBed.createComponent(ChatQueueComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders waiting sessions and claims session on click', () => {
    const fixture = render();
    const req = http.expectOne((request) => request.url === '/api/chat/waiting');
    expect(req.request.method).toBe('GET');
    req.flush(
      envelope(paged([
        {
          id: 's1',
          customerName: 'Sarah Connor',
          customerEmail: 'sarah@skynet.com',
          status: 'Waiting',
          type: 'Web',
          priority: 'Normal',
          createdAt: '2026-08-27T10:00:00Z',
        },
      ])),
    );
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Sarah Connor');

    const navigateSpy = vi.spyOn(router, 'navigate');
    fixture.componentInstance.claim({
      id: 's1',
      status: 'Waiting',
      type: 'Web',
      priority: 'Normal',
      createdAt: '2026-08-27T10:00:00Z',
    });

    const claimReq = http.expectOne('/api/chat/sessions/s1/claim');
    expect(claimReq.request.method).toBe('POST');
    claimReq.flush(envelope({ id: 's1', status: 'Active' }));

    expect(navigateSpy).toHaveBeenCalledWith(['/chat/sessions', 's1']);
  });

  it('shows empty state when no sessions are waiting', () => {
    const fixture = render();
    http.expectOne((request) => request.url === '/api/chat/waiting').flush(envelope(paged([])));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('No waiting sessions');
  });

  it('does not try to claim an already active session', () => {
    const fixture = render();
    http.expectOne((request) => request.url === '/api/chat/waiting').flush(
      envelope(paged([
        {
          id: 's2',
          customerName: 'Active Customer',
          status: 'Active',
          type: 'Web',
          priority: 'Normal',
          createdAt: '2026-08-27T10:00:00Z',
        },
      ])),
    );
    fixture.detectChanges();

    fixture.componentInstance.claim({
      id: 's2',
      status: 'Active',
      type: 'Web',
      priority: 'Normal',
      createdAt: '2026-08-27T10:00:00Z',
    });

    http.expectNone('/api/chat/sessions/s2/claim');
    expect(fixture.nativeElement.textContent).toContain('Open session');
  });
});
