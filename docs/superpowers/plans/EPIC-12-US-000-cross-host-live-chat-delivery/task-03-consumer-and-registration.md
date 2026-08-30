# task-03 — Consumer (both hosts) + registration

**Status:** Complete
**AC:** CC-30, CC-31, CC-32, CC-34

## Change
- New `Infrastructure/Messaging/Consumers/ChatMessagePushedConsumer.cs`:
  `IConsumer<ChatMessagePushed>`; injects `IRealTimeNotifier`, `ChatMessagePushedDeduplicator`,
  `ILogger`. Consume: `TryMark(MessageId)` → if already seen, drop (CC-32); else map to
  `ChatMessagePushPayload` and `NotifyChatMessageAsync` (reaches the consuming host's own live
  `/hubs/chat` group → CC-30/CC-31; staff `/hubs/main` → CC-34).
- New `Infrastructure/Messaging/Consumers/ChatMessagePushedDeduplicator.cs`:
  in-memory, bounded, keyed on `MessageId`, 5-minute window (CC-32).
- `Infrastructure/ServiceCollectionExtensions.cs`: registered
  `AddSingleton<ChatMessagePushedDeduplicator>()` before the credential branch (so it exists even in
  NoOp/messaging-off mode) and `x.AddConsumer<ChatMessagePushedConsumer>()` in the MassTransit block.

## Evidence
- Consumer + dedup compile; exercised by the integration tests in task-04.
- When no bus is present (`NoOpMessagePublisher`), nothing is published and nothing pushed; the
  transcript remains the fallback (CC-33).
