# Addendum: Cross-Host Live-Chat Delivery (FEAT-26 gap closure)

**Date:** 2026-08-29
**Status:** Approved 2026-08-29
**Feature:** `FEAT-26` (Live chat)
**Related ACs:** `CC-14`, `CC-15`, `CC-16`, `CC-19`, `A12`
**Supersedes/extends:** `EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md` §Live chat and §API host split

## Problem

The existing live-chat design delivers a chat message in real time only **within a single process**.
CC-16 requires that when either party sends a message it is delivered to the other in real time (no
polling), and CC-19/`A12` split the surface across two hosts:

| Surface | Host |
|---|---|
| Live chat — customer side (`/hubs/chat`, start/send/receive) | `ExternalApi` |
| Live chat — agent side (queue, claim, reply, convert) | `InternalApi` |

SignalR delivers over an `IHubContext<T>` that is **process-local**. `RealTimeNotifier` (Api.Shared)
pushes to the `chat:{sessionId}` group of the `ChatHub` in *its own process* (`IHubContext<ChatHub>`).
Today the agent's reply is recorded and pushed from the `InternalApi` process
(`SendAgentChatMessageCommandHandler`, `LiveChatFeatures.cs:199`), whose `/hubs/chat` group is empty —
the customer is connected to `ExternalApi`'s `/hubs/chat`. The push therefore never reaches the
customer. This was confirmed live: the WebSocket connect, claim (`Waiting→Active`) and agent send all
succeed, yet no `ChatMessageReceived` arrives on the connected customer's `/hubs/chat` connection.

## Assumptions

- **A1:** The two hosts remain separate processes (ADR-0008); the fix must not collapse live chat
  onto a single host or move the agent surface onto `ExternalApi`.
- **A2:** The existing messaging seam (`IMessagePublisher`, MassTransit, topic constants in
  `Shared.Contracts/Topics.cs`, and the `NotificationMessageConsumer`/`EmailMessageConsumer` pattern)
  is the project's sanctioned cross-process transport (NG-9's "reuse the seam" goal).
- **A3:** When no message bus is configured (`NoOpMessagePublisher`, the local default in
  `Development`), real-time delivery degrades gracefully: the send still persists and succeeds, and
  the widget may recover missed messages from the transcript endpoint. This mirrors how email/SMS
  outbound already degrades when RabbitMQ is absent.
- **A4:** A chat message is delivered to the customer exactly once, in order relative to other
  messages in the same session. The pump is at-least-once with an idempotent consumer (keyed on the
  message id), matching the existing gateway retry semantics.
- **A5:** The agent's own transcript stays correct regardless of the pump: the agent message is
  persisted atomically before any event is published (same rule as NG-5 / `InAppNotificationChannelSender`).

## Out of scope

- Typing indicators and session→ticket conversion UI (already excluded by the FEAT-26 frontend spec).
- A SignalR Redis/Azure backplane or any cross-host scaleout of `/hubs/chat` itself — the pump carries
  the *event*; each host still pushes to its own local hub connections.
- Changing `MainHub` or the authenticated `/hubs/main` broadcast shape.

## Acceptance criteria

- **CC-30:** Given an `Active` live-chat session and a message bus configured, when an agent sends a
  message through the `InternalApi` host, then a `ChatMessagePushed` event is published on the shared
  bus; the `ExternalApi` host consumes it and pushes `ChatMessageReceived` to the `chat:{sessionId}`
  group of its own `/hubs/chat`, so the connected customer receives it in real time.
- **CC-31:** Given a `/hubs/chat` connection, when a `ChatMessagePushed` event carries a message for
  that connection's session, then the customer receives exactly the sender type/name/body of that
  message; a message for another session is never delivered to this connection (scoped by group).
- **CC-32:** Given a duplicate `ChatMessagePushed` event (retry), when it is consumed again, then the
  customer is not sent the same message twice (consumer is idempotent on the message id).
- **CC-33:** Given no message bus configured (local default), when an agent sends a message, then the
  message is still persisted and the agent's REST response still succeeds; the customer-side push is
  not attempted, so a connected client that reloads recovers the message from the transcript endpoint
  (`GET /api/external/chat/{session}/messages`).
- **CC-34:** Given a customer-authored message sent by an `ExternalApi` REST call, when it is
  delivered, then it reaches the connected customer's own connection (for the author's other open
  views) and the agent's `MainHub` clients, without a round-trip through the bus being required for
  the author's session.

## Design

### Shared contract

Add to `CustomerSupport.Shared.Contracts`:

```csharp
public record ChatMessagePushed(
    Guid MessageId,
    Guid SessionId,
    string SenderType,   // Customer | Agent
    string SenderName,
    Guid? SenderId,
    string Body,
    DateTimeOffset SentAt);
```

and a topic constant `Topics.ChatMessagesPushed => "chat.messages.pushed"`, following the
`sla.messages.escalated` precedent. The record shape is deliberately the same as the existing
`ChatMessagePushPayload` so the boundary type is a faithful copy and the consumer can map it 1:1.

### Publish side (internal)

`SendAgentChatMessageCommandHandler` (LiveChatFeatures.cs) already decides the agent reply. After the
message is persisted (`SaveChangesAsync`) and the local in-process `NotifyChatMessageAsync` runs
(serviceable staff push inside the same process), publish the cross-host event via `IMessagePublisher`
(injected into the handler) with `Topics.ChatMessagesPushed`. The local push remains harmless; the
bus event is what reaches the customer on `ExternalApi`. Persisted-before-publish guarantees A5.

The customer authoring a message (ExternalApi) does **not** publish to the bus for its own session
(CC-34): its own process holds the customer's `/hubs/chat` connection and the agent `MainHub` clients
live on the same InternalApi that also needs the event — so it publishes too.

### Consume side (external)

Add `ChatMessagePushedConsumer : IConsumer<ChatMessagePushed>` in Infrastructure/Messaging/Consumers.
It injects `IRealTimeNotifier` (scoped) and maps the contract to a `ChatMessagePushPayload`, then calls
`NotifyChatMessageAsync` — which, running in the `ExternalApi` process, pushes to the *live* customer
connections on that host's `/hubs/chat`. Idempotency (CC-32): a tiny dedupe check keyed on
`MessageId` (e.g. `IRepository<LiveChatMessage>` already contains the row; consumer skips push if it
has already been signalled, or relies on the push being a read-only duplicate). Because the same host
also publishes for customer-authored messages, the consumer runs on both hosts.

Register the consumer in `ConfigureMessaging` (ServiceCollectionExtensions.cs) beside
`NotificationMessageConsumer`/`EmailMessageConsumer`/`SmsMessageConsumer`, so it activates whenever
RabbitMQ credentials are present, and returns early when the no-op publisher is in use (CC-33).

### Error and fallback

- Publish failures and consumer exceptions flow through the existing MassTransit retry policy
  (already configured, `ConfigureMessaging`), which also provides the at-least-once duplication that
  CC-32's idempotency absorbs.
- No new HTTP endpoint, validation rule, or envelope shape is introduced.

## Test strategy

- Backend integration (`LiveChatHubEndpointTests`): drive the publish→consumer→hub path. Because the
  test hosts are in-process, the existing `PushAgentMessageAsync` path already exercises the local
  push; add a test that publishes `ChatMessagePushed` through the same factory's service provider and
  asserts the connected `/hubs/chat` client receives it (CC-30/CC-31), plus a duplicate-publish test
  asserting a single delivery (CC-32). Name tests after the CC they satisfy.
- A unit test for the topic constant and for `ChatMessagePushedConsumer` idempotency.
