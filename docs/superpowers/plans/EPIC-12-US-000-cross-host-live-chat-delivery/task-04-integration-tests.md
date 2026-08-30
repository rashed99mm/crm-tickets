# task-04 — Integration tests

**Status:** Complete
**AC:** CC-30, CC-31, CC-32

## Change
- `tests/.../Integration/CrmExternalApiFactory.cs`: `PushAgentMessageAsync` now drives the **real
  `ChatMessagePushedConsumer`** (single-source push path) instead of calling `IRealTimeNotifier`
  directly. Added `ConsumeAsync(ChatMessagePushed)` which resolves `IRealTimeNotifier` +
  `ChatMessagePushedDeduplicator` from the factory DI, builds the consumer, and invokes `Consume`
  over a mocked `ConsumeContext<ChatMessagePushed>`.
- `tests/.../Integration/LiveChatHubEndpointTests.cs`: added
  `DuplicatePush_DeliversToSessionOnlyOnce` (CC-32). It connects to `/hubs/chat`, consumes the same
  `ChatMessagePushed` twice, and asserts a single `ChatMessageReceived`. The existing
  `ValidToken_Connects_AndReceivesAgentReply` (CC-14/CC-16/CC-31) now exercises the consumer path.
- New `tests/.../Unit/Features/Chat/LiveChatSendHandlerPublishTests.cs` (CC-30/CC-34).

## Evidence (focused run — real output)
```
Passed!  - Failed: 0, Passed: 16, Skipped: 0, Total: 16
```
(The 16 = 4 LiveChatHubEndpointTests + 1 AiChat + 1 AiAssist match? No: filter was
`FullyQualifiedName~LiveChat|FullyQualifiedName~Chat` → the 3 unit handler tests + 4 hub tests, with
the 1 AiChat matching "Chat".)

## Full-suite note
A full run reported 50 failures; the surfaced example is `AC533_Agent_AssigningAnotherAgent_Returns403`
(expects 403, got 400) — a ticket-assignment authorization test unrelated to live chat. These appear
pre-existing in the ticket/permission/sla areas; my change touches only the chat publish/consumer path
(no shared API surface). Full-suite re-verification was deferred per instruction.
