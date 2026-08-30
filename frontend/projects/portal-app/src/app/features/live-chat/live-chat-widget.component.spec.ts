import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { vi } from 'vitest';
import {
  ChatMessageDto,
  envelopeInterceptor,
  LiveChatRealtimeService,
  LiveChatConnectionState,
} from 'common';
import LiveChatWidgetComponent from './live-chat-widget.component';

function envelope(data: unknown) {
  return { success: true, code: 'OK', message: 'OK', data, errors: [] };
}

class FakeRealtimeService {
  readonly state = signal<LiveChatConnectionState>('disconnected');
  readonly incoming = signal<ChatMessageDto | null>(null);
  connect = vi.fn().mockResolvedValue(undefined);
  disconnect = vi.fn().mockResolvedValue(undefined);
}

describe('LiveChatWidgetComponent', () => {
  let http: HttpTestingController;
  let realtime: FakeRealtimeService;

  beforeEach(() => {
    realtime = new FakeRealtimeService();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        { provide: LiveChatRealtimeService, useValue: realtime },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function render(): ComponentFixture<LiveChatWidgetComponent> {
    const fixture = TestBed.createComponent(LiveChatWidgetComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('starts anonymous chat session and allows sending messages', () => {
    const fixture = render();
    fixture.componentInstance.startForm.setValue({
      customerName: 'Bruce Wayne',
      customerEmail: 'bruce@wayne.com',
      initialMessage: 'Where is the Batmobile?',
    });

    fixture.componentInstance.startChat();

    const startReq = http.expectOne('/api/external/chat/start');
    expect(startReq.request.method).toBe('POST');
    expect(startReq.request.body.customerName).toBe('Bruce Wayne');
    startReq.flush(envelope({ sessionToken: 'tok-secret-123', sessionId: 'sess-999' }));
    fixture.detectChanges();

    expect(fixture.componentInstance.started()).toBe(true);
    expect(fixture.componentInstance.messages().length).toBe(1);

    fixture.componentInstance.messageForm.setValue({ body: 'Any updates?' });
    fixture.componentInstance.send();

    const msgReq = http.expectOne('/api/external/chat/messages');
    expect(msgReq.request.method).toBe('POST');
    expect(msgReq.request.body.token).toBe('tok-secret-123');
    expect(msgReq.request.body.body).toBe('Any updates?');
    msgReq.flush(
      envelope({
        id: 'm-2',
        sessionId: 'sess-999',
        senderType: 'Customer',
        senderName: 'Bruce Wayne',
        body: 'Any updates?',
        sentAt: '2026-08-27T10:05:00Z',
      }),
    );
    fixture.detectChanges();

    expect(fixture.componentInstance.messages().length).toBe(2);
  });

  it('connects to the session anonymous hub with the opaque token after start (FB-4)', () => {
    const fixture = render();
    fixture.componentInstance.startForm.setValue({
      customerName: 'Bruce Wayne',
      customerEmail: 'bruce@wayne.com',
      initialMessage: 'Hi',
    });
    fixture.componentInstance.startChat();
    http.expectOne('/api/external/chat/start').flush(
      envelope({ sessionToken: 'tok-secret-123', sessionId: 'sess-999' }),
    );
    fixture.detectChanges();

    expect(realtime.connect).toHaveBeenCalledWith('tok-secret-123');
  });

  it('appends an agent push received from the hub, scoped to the active session (FB-5)', () => {
    const fixture = render();
    fixture.componentInstance.startForm.setValue({
      customerName: 'Bruce Wayne',
      customerEmail: 'bruce@wayne.com',
      initialMessage: 'Hi',
    });
    fixture.componentInstance.startChat();
    http.expectOne('/api/external/chat/start').flush(
      envelope({ sessionToken: 'tok-secret-123', sessionId: 'sess-999' }),
    );
    fixture.detectChanges();

    realtime.incoming.set({
      id: 'm-push-1',
      sessionId: 'sess-999',
      senderType: 'Agent',
      senderName: 'Support',
      body: 'Welcome to the Batcave!',
      sentAt: '2026-08-27T10:10:00Z',
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.messages().map((m) => m.id)).toContain('m-push-1');

    // Pushes from another session are ignored.
    realtime.incoming.set({
      id: 'm-push-2',
      sessionId: 'sess-OTHER',
      senderType: 'Agent',
      senderName: 'Support',
      body: 'Ignore me',
      sentAt: '2026-08-27T10:11:00Z',
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.messages().map((m) => m.id)).not.toContain('m-push-2');
  });

  it('disconnects from the hub when the chat ends', () => {
    const fixture = render();
    fixture.componentInstance.endChat();
    expect(realtime.disconnect).toHaveBeenCalled();
  });
});
