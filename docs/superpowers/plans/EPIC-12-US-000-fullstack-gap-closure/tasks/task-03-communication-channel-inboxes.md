# Task 03 - Communication Channel Inboxes

**Status:** In progress  
**Closes gaps:** Email inbound UI, WhatsApp session UI, SMS UI, static chat session, chat KB links.

## Files

- Backend domain: `Entities/Tickets/TicketMessage.cs`, `Entities/Channels/**`
- Backend API: `ExternalApiController.cs`, new/extended `ChannelsController.cs`
- Realtime: `MainHub.cs`, `RealTimeNotifier.cs`
- Frontend API/store: `common/src/lib/channels/*`
- Frontend UI: `admin-app/src/app/features/chat/*`, ticket message timeline

## Implementation

- Create unified channel conversation query model.
- Add channel filters and conversation detail endpoints.
- Push `ChannelMessageReceived` through SignalR.
- Add inbox tabs for Email, WhatsApp, SMS, Live Chat, Web Form.
- Wire reply composer to channel send endpoint.

## Code Example

```csharp
public sealed record SendChannelReplyCommand(
    Guid ConversationId,
    string Body,
    string Channel) : IRequest<Response<ChannelMessageDto>>;
```

```ts
readonly conversations = signal<AsyncState<readonly ChannelConversation[]>>(loading());

sendReply(conversationId: string, body: string): Observable<ChannelMessage> {
  return this.http.post<ChannelMessage>(`/api/channels/conversations/${conversationId}/messages`, { body });
}
```

## Acceptance

- [ ] Channel cards are real inboxes, not static summaries.
- [ ] Agent can filter by channel/status/search.
- [x] Conversation detail loads transcript from API.
- [x] Replies persist and append to transcript.
- [x] Realtime push updates open conversation.
- [ ] KB links navigate to existing route only.

## Evidence

- Implemented `LiveChatSession` and `LiveChatMessage` domain entities plus EF configurations.
- Added staff endpoints:
  - `GET /api/chat/waiting`
  - `POST /api/chat/sessions/{sessionId}/claim`
  - `GET /api/chat/sessions/{sessionId}/messages`
  - `POST /api/chat/sessions/{sessionId}/messages`
  - `POST /api/chat/sessions/{sessionId}/close`
- Added anonymous customer endpoints:
  - `POST /api/external/chat/start`
  - `POST /api/external/chat/messages`
  - `GET /api/external/chat/messages?token=...`
- Added EF migration `20260829214700_AddLiveChatSessions` for durable live-chat sessions/messages.
- Added `IRealTimeNotifier.NotifyChatMessageAsync` and `ChatMessageReceived` SignalR broadcast after customer/agent live-chat messages are saved. The existing Angular `ChatStore` listener appends matching session messages with duplicate protection.
- Verified common API test: `npx ng test common --watch=false --include=projects/common/src/lib/channels/chat.api.spec.ts` passed 5 tests.
