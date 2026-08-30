import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import ChatSessionComponent from './chat-session.component';

function envelope(data: unknown) {
  return { success: true, code: 'OK', message: 'OK', data, errors: [] };
}

describe('ChatSessionComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function render(): ComponentFixture<ChatSessionComponent> {
    const fixture = TestBed.createComponent(ChatSessionComponent);
    fixture.componentRef.setInput('id', 'sess-123');
    fixture.detectChanges();
    return fixture;
  }

  it('hydrates transcript and sends reply', () => {
    const fixture = render();
    const req = http.expectOne('/api/chat/sessions/sess-123/messages');
    expect(req.request.method).toBe('GET');
    req.flush(
      envelope([
        {
          id: 'm1',
          sessionId: 'sess-123',
          senderType: 'Customer',
          senderName: 'Alice',
          body: 'Hello',
          sentAt: '2026-08-27T10:00:00Z',
        },
      ]),
    );
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Alice');
    expect(el.textContent).toContain('Hello');

    fixture.componentInstance.messageForm.setValue({ body: 'Hi Alice!' });
    fixture.componentInstance.send();

    const sendReq = http.expectOne('/api/chat/sessions/sess-123/messages');
    expect(sendReq.request.method).toBe('POST');
    expect(sendReq.request.body).toEqual({ body: 'Hi Alice!' });
    sendReq.flush(
      envelope({
        id: 'm2',
        sessionId: 'sess-123',
        senderType: 'Agent',
        senderName: 'Agent Smith',
        body: 'Hi Alice!',
        sentAt: '2026-08-27T10:01:00Z',
      }),
    );
    fixture.detectChanges();

    expect(el.textContent).toContain('Hi Alice!');
  });

  it('loads AI reply drafts and inserts one into the composer', () => {
    const fixture = render();
    http.expectOne('/api/chat/sessions/sess-123/messages').flush(
      envelope([
        {
          id: 'm1',
          sessionId: 'sess-123',
          senderType: 'Customer',
          senderName: 'Alice',
          body: 'I cannot log in',
          sentAt: '2026-08-27T10:00:00Z',
        },
      ]),
    );

    fixture.componentInstance.loadAiSuggestions();
    const aiReq = http.expectOne('/api/chat/sessions/sess-123/ai/reply');
    expect(aiReq.request.method).toBe('POST');
    aiReq.flush(
      envelope({
        drafts: ['Hi Alice, I am checking your login issue now.'],
        summary: '1 customer message(s), 0 agent reply/replies. Latest customer note: I cannot log in',
      }),
    );
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Hi Alice, I am checking your login issue now.');

    fixture.componentInstance.insertDraft('Hi Alice, I am checking your login issue now.');
    expect(fixture.componentInstance.messageForm.controls.body.value).toBe(
      'Hi Alice, I am checking your login issue now.',
    );
  });
});
