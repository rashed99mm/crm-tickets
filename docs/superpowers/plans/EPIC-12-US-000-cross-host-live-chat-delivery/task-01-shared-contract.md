# task-01 — Shared contract + topic

**Status:** Complete
**AC:** CC-30, CC-31, CC-32, CC-33, CC-34 (transport contract)

## Change
- New `CustomerSupport.Shared.Contracts/Messages/ChatMessagePushed.cs`:
  `record ChatMessagePushed(Guid MessageId, Guid SessionId, string SenderType, string SenderName,
  Guid? SenderId, string Body, DateTime SentAt)`. `SentAt` is `DateTime` to match
  `ChatMessagePushPayload.SentAt` and the persisted `LiveChatMessage.SentAt`.
- Added `Topics.ChatMessagesPushed => "chat.messages.pushed"` in `Shared.Contracts/Topics.cs`.

## Evidence
- Contract and topic compile; consumed by task-02 (handlers) and task-03 (consumer).
- `Application.csproj` references `Shared.Contracts` (line 14), so the handlers can publish the
  contract without violating the dependency rule.
