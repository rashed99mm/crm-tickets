# task-02 — Publish from the two send handlers

**Status:** Complete
**AC:** CC-30 (agent→ExternalApi), CC-34 (customer→agent)

## Change
`backend/src/CustomerSupport.Application/Features/Chat/LiveChatFeatures.cs`
- `SendAgentChatMessageCommandHandler`: ctor now injects `IMessagePublisher publisher` (dropped
  `IRealTimeNotifier realtime`); `Handle` persists then
  `await publisher.PublishAsync(Topics.ChatMessagesPushed, new ChatMessagePushed(...))` instead of
  `realtime.NotifyChatMessageAsync(ToPush(message))`; removed the private `ToPush` helper.
- `SendAnonymousChatMessageCommandHandler`: same replacement (publish instead of direct push).
- Added `using CustomerSupport.Shared.Contracts;` and `...Shared.Contracts.Messages;`.

This is the recorded deviation: the handlers publish; the **consumer** is the single source of the
real-time push, so no MainHub double-delivery on the internal host.

## Evidence
- Unit tests `LiveChatSendHandlerPublishTests` (3) assert each handler publishes a `ChatMessagePushed`
  carrying the persisted session id, and that publish follows `SaveChangesAsync`.
- Verified by the focused run: 16/16 chat tests pass.
