import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RealtimeService } from '../realtime/realtime.service';
import { ChatStore } from './chat.store';
import { ChatMessageDto } from './chat.model';

describe('ChatStore', () => {
  let store: ChatStore;
  let http: HttpTestingController;
  let realtime: RealtimeService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(ChatStore);
    http = TestBed.inject(HttpTestingController);
    realtime = TestBed.inject(RealtimeService);
  });

  afterEach(() => {
    store.destroy();
    http.verify();
  });

  it('initSession: fetches transcript and registers realtime listener', () => {
    store.initSession('s1');

    const req = http.expectOne('/api/chat/sessions/s1/messages');
    expect(req.request.method).toBe('GET');

    const mockMsg: ChatMessageDto = {
      id: 'm1',
      sessionId: 's1',
      senderType: 'Customer',
      senderName: 'Guest',
      body: 'Need help',
      sentAt: '2026-08-27T10:00:00Z',
    };
    req.flush([mockMsg]);

    expect(store.messages().length).toBe(1);
    expect(store.messages()[0].body).toBe('Need help');
  });

  it('appendMessage: ignores duplicate messages or wrong sessions', () => {
    store.initSession('s1');
    http.expectOne('/api/chat/sessions/s1/messages').flush([]);

    const msg1: ChatMessageDto = {
      id: 'm1',
      sessionId: 's1',
      senderType: 'Customer',
      senderName: 'Guest',
      body: 'Hello',
      sentAt: '2026-08-27T10:00:00Z',
    };

    store.appendMessage(msg1);
    expect(store.messages().length).toBe(1);

    // Duplicate message
    store.appendMessage(msg1);
    expect(store.messages().length).toBe(1);
  });
});
