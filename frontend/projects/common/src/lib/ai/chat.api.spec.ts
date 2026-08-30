import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AiChatApi } from './chat.api';
import { envelopeInterceptor } from '../api/envelope.interceptor';

describe('AiChatApi', () => {
  let http: HttpTestingController;
  let api: AiChatApi;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(AiChatApi);
  });

  afterEach(() => http.verify());

  it('starts a session and unwraps the conversation', () => {
    let chat: object | undefined;
    api.start('I cannot sign in').subscribe((c) => (chat = c));

    const req = http.expectOne('/api/ai/chats');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ message: 'I cannot sign in' });
    req.flush({
      sessionId: 'c1',
      status: 'Open',
      ticketId: null,
      turns: [{ id: 't1', role: 'assistant', body: 'Try this.', citations: [] }],
    });

    expect(chat).toEqual({
      sessionId: 'c1',
      status: 'Open',
      ticketId: null,
      turns: [{ id: 't1', role: 'assistant', body: 'Try this.', citations: [] }],
    });
  });

  it('sends follow-up turns to the session route', () => {
    let chat: object | undefined;
    api.send('c1', 'and what about billing?').subscribe((c) => (chat = c));

    const req = http.expectOne('/api/ai/chats/c1/messages');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ message: 'and what about billing?' });
    req.flush({ sessionId: 'c1', status: 'Open', ticketId: null, turns: [] });

    expect(chat).toBeDefined();
  });

  it('requests handoff on the session route', () => {
    let ticketId: string | undefined;
    api.handoff('c1', 'cust-1', 'cat-1').subscribe((id) => (ticketId = id));

    const req = http.expectOne('/api/ai/chats/c1/handoff');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ customerId: 'cust-1', categoryId: 'cat-1' });
    req.flush('ticket-9');

    expect(ticketId).toBe('ticket-9');
  });
});
