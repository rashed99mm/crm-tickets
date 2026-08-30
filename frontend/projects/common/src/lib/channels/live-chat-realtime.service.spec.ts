import { TestBed } from '@angular/core/testing';
import { LIVE_CHAT_HUB_PATH, liveChatHubUrl, LiveChatRealtimeService } from './live-chat-realtime.service';

describe('LiveChatRealtimeService', () => {
  let service: LiveChatRealtimeService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [LiveChatRealtimeService] });
    service = TestBed.inject(LiveChatRealtimeService);
  });

  it('disconnect is safe when never connected', async () => {
    await service.disconnect();
    expect(service.state()).toBe('disconnected');
    expect(service.incoming()).toBeNull();
  });

  it('builds the hub url carrying only the opaque session token (FB-8)', () => {
    const url = liveChatHubUrl('an+opaque3TokenValue_123');

    expect(url).toBe(`${LIVE_CHAT_HUB_PATH}?token=an%2Bopaque3TokenValue_123`);
    expect(url).not.toContain('customer');
    expect(url).not.toContain('ticket');
    expect(url).not.toContain('@');
    expect(url).not.toContain('senderId');
  });

  it('reports disconnected when the hub cannot be reached', async () => {
    // In jsdom there is no /hubs/chat endpoint, so connect can never become `connected`; it must
    // settle on `disconnected` (surfaced through `state`) rather than throw out of the call.
    await service.connect('token-a');
    expect(service.state()).toBe('disconnected');
  });
});
