# Backend Plan — Cross-Host Live-Chat Delivery (MassTransit pump)

**Spec:** `docs/superpowers/specs/EPIC-12-US-000-cross-host-live-chat-delivery-addendum.md` (approved)
**Feature:** `FEAT-26` live chat — CC-30, CC-31, CC-32, CC-33, CC-34
**Date:** 2026-08-29

## Grounding facts (cited)

- The agent push today: `SendAgentChatMessageCommandHandler.Handle` persists then calls
  `realtime.NotifyChatMessageAsync(ToPush(message))` at
  `backend/src/CustomerSupport.Application/Features/Chat/LiveChatFeatures.cs:199` (ctor injects
  `IRealTimeNotifier realtime` at :173).
- The customer push today: `SendAnonymousChatMessageCommandHandler.Handle` persists then calls
  `realtime.NotifyChatMessageAsync(ToPush(message))` at the same file `:307` (ctor injects `realtime`
  at :272).
- `RealTimeNotifier.NotifyChatMessageAsync` pushes to **its own process**'s `IHubContext<ChatHub>`
  group `chat:{sessionId}` and `IHubContext<MainHub>` `Clients.All`
  (`backend/src/CustomerSupport.Api.Shared/Notifications/RealTimeNotifier.cs:30-40`).
- SignalR `IHubContext` is process-local. Customers connect to `ExternalApi`'s `/hubs/chat`; agents
  work in `InternalApi`. The InternalApi push reaches nobody, confirmed live.
- `ChatMessagePushPayload(Id, SessionId, SenderType, SenderName, SenderId, Body, SentAt)` at
  `Application/Notifications/Contracts.cs:33-40`; `IRealTimeNotifier.NotifyChatMessageAsync(ChatMessagePushPayload)`
  at `Contracts.cs:71`.
- `IMessagePublisher.PublishAsync<T>(string topic, T message, ct)` at
  `Application/Interfaces/IMessagePublisher.cs:3-6`. Registered **scoped** in
  `Infrastructure/ServiceCollectionExtensions.cs:198` (`NoOpMessagePublisher`) and `:227`
  (`MassTransitMessagePublisher`). Both hosts call `ConfigureMessaging`.
- Shared contracts: `record`s in `Shared.Contracts/Messages/*.cs`; topic constants in
  `Shared.Contracts/Topics.cs` (e.g. `SlaEscalated => "sla.messages.escalated"`).
- Consumer registration: `ServiceCollectionExtensions.cs:204-206` (`x.AddConsumer<NotificationMessageConsumer>()`
  etc.) + `:223` `cfg.ConfigureEndpoints(context)`.
- `Application.csproj` already references `Shared.Contracts` (line 14).
- Constants: `NotificationGatewayConstants.SignalRChatMessageMethod = "ChatMessageReceived"`,
  `ChatSessionGroup(Guid)` at `Application/Notifications/NotificationGatewayConstants.cs:29,36`.

## Design (refined from spec; deviation recorded)

The approved spec said "keep the local push + publish, consumer runs on both hosts." Direct analysis
proves that double-delivers: if the agent handler keeps its direct `NotifyChatMessageAsync` **and**
InternalApi also runs the consumer, staff on `/hubs/main` receive the same message twice (handler push
+ consumer push). **Deviation:** make the bus consumer the **single source** of the real-time push. Both
send handlers **publish** `ChatMessagePushed` (after persist) instead of pushing directly; the consumer
on each host pushes to that host's locally-owned hub (`NotifyChatMessageAsync` already targets both
hubs — each host's "wrong" hub is empty, so no spurious delivery). No-bus (`NoOpMessagePublisher`) →
no real-time push, message still persisted and retrievable from transcript (CC-33).

## Tasks (each → its AC, one commit)

### Task 01 — Shared contract + topic
Files: `Shared.Contracts/Messages/ChatMessagePushed.cs` (new), `Shared.Contracts/Topics.cs` (edit).
- `public record ChatMessagePushed(Guid MessageId, Guid SessionId, string SenderType, string SenderName,
  Guid? SenderId, string Body, DateTimeOffset SentAt);`
- `Topics.ChatMessagesPushed => "chat.messages.pushed";`
AC: CC-30, CC-31, CC-32, CC-33, CC-34 (transport contract). Test: `ChatMessagePushed_HasTopicConstant`.

### Task 02 — Publish from the two send handlers
Files: `Application/Features/Chat/LiveChatFeatures.cs`.
- Add `IMessagePublisher publisher` (using already present `Application.Interfaces` at :5) to
  `SendAgentChatMessageCommandHandler` ctor (:167-175) and `SendAnonymousChatMessageCommandHandler`
  ctor (:268-273); drop `IRealTimeNotifier realtime` from both.
- In both `Handle`s, replace the `realtime.NotifyChatMessageAsync(ToPush(message), ct)` call (agent
  `:199`; customer `:307`) with
  `await publisher.PublishAsync(Topics.ChatMessagesPushed, new ChatMessagePushed(message.Id, message.SessionId,
  message.SenderType, message.SenderName, message.SenderId, message.Body, message.SentAt), ct);`
  and remove the now-unused private `ToPush(LiveChatMessage)` helpers (`:204-211`, `:312-319`).
- Keep `using CustomerSupport.Shared.Contracts;` (add if absent).
AC: CC-30 (agent→ExternalApi), CC-34 (customer sends publish). Tests: unit tests that each handler
publishes a `ChatMessagePushed` carrying the persisted message's fields after `SaveChangesAsync`.

### Task 03 — Consumer (both hosts) + register
Files: `Infrastructure/Messaging/Consumers/ChatMessagePushedConsumer.cs` (new),
`Infrastructure/Messaging/ChatMessagePushedDeduplicator.cs` (new, if kept in Infra),
`Infrastructure/ServiceCollectionExtensions.cs` (edit), `Api.Shared` DI for the dedup store.
- `ChatMessagePushedConsumer : IConsumer<ChatMessagePushed>` injects `IRealTimeNotifier` (scoped) and
  a dedup guard. `Consume`: if the `MessageId` was already pushed, return (CC-32); else map to
  `ChatMessagePushPayload` and `await realtime.NotifyChatMessageAsync(payload)` — running in the
  consuming host, it reaches that host's live `/hubs/chat` group (CC-30/CC-31) or `/hubs/main` (CC-34).
- Register `x.AddConsumer<ChatMessagePushedConsumer>()` beside the others in
  `ServiceCollectionExtensions.cs:204-206`. Register the dedup guard as scoped/singleton via the
  Infrastructure `AddPlatformInfrastructureServices`.
AC: CC-30, CC-31, CC-32, CC-34. Tests: integration (below) + a consumer idempotency test.

### Task 04 — Integration tests (extend `LiveChatHubEndpointTests`)
- `AgentSend_CrossHostPumpsToConnectedCustomer` (CC-30): start anonymous (ExternalApi), connect
  `/hubs/chat`, publish `ChatMessagePushed` through the factory's `IMessagePublisher`, assert the
  connected client receives `ChatMessageReceived` with the right body/sender.
- `DuplicatePublish_DeliversOnce` (CC-32): publish the same `ChatMessagePushed` twice, assert a single
  delivery.
- `NoNotificationToOtherSession` already covers CC-31 scoping.
- Because both hosts are one in-process factory here, this exercises publish→consumer→hub in-process;
  the true cross-process cut is covered by the live two-host run afterwards.
AC: CC-30, CC-31, CC-32.

## Order
01 → 02 → 03 → 04. Each task is one commit with a failing test first.

## Shipped check
After implementation: `dotnet test CustomerSupport.slnx` green (paste output), `dotnet build -warnaserror`
clean, re-run the live two-host `LiveChatVerify` tool expecting **PASS**, update task records + README,
commit per task.
