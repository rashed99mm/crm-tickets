import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ChatApi } from './chat.api';
import { ChatSessionDto, ChatMessageDto } from './chat.model';
import { PagedResult } from '../api/api-response';

describe('ChatApi', () => {
  let api: ChatApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ChatApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getWaitingSessions: GETs /api/chat/waiting with pagination params', () => {
    let result: PagedResult<ChatSessionDto> | undefined;
    api.getWaitingSessions({ page: 2, pageSize: 10, status: 'Waiting', search: 'Sarah', sortBy: 'createdAt', sortDirection: 'asc' }).subscribe((res) => (result = res));

    const req = http.expectOne('/api/chat/waiting?page=2&pageSize=10&status=Waiting&search=Sarah&sortBy=createdAt&sortDirection=asc');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [{ id: 's1', status: 'Waiting', priority: 'Normal', type: 'Chat', createdAt: '2026-08-27T10:00:00Z' }], pageIndex: 2, pageSize: 10, totalCount: 1 });

    expect(result?.items.length).toBe(1);
    expect(result?.items[0].id).toBe('s1');
    expect(result?.totalCount).toBe(1);
  });

  it('claimSession: POSTs to /api/chat/sessions/:id/claim', () => {
    api.claimSession('s1').subscribe();
    const req = http.expectOne('/api/chat/sessions/s1/claim');
    expect(req.request.method).toBe('POST');
    req.flush({ id: 's1', status: 'Active' });
  });

  it('sendMessage: POSTs to /api/chat/sessions/:id/messages with body', () => {
    api.sendMessage('s1', 'Hello there').subscribe();
    const req = http.expectOne('/api/chat/sessions/s1/messages');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ body: 'Hello there' });
    req.flush({ id: 'm1', sessionId: 's1', body: 'Hello there' });
  });

  it('suggestReply: POSTs to /api/chat/sessions/:id/ai/reply', () => {
    api.suggestReply('s1').subscribe();
    const req = http.expectOne('/api/chat/sessions/s1/ai/reply');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({ drafts: ['Thanks for the detail.'], summary: '1 customer message.' });
  });

  it('startAnonymousSession: POSTs to /api/external/chat/start', () => {
    api.startAnonymousSession({ customerName: 'Guest', customerEmail: 'g@test.local' }).subscribe();
    const req = http.expectOne('/api/external/chat/start');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.customerName).toBe('Guest');
    req.flush({ sessionToken: 'tok-1', sessionId: 's1' });
  });
});
