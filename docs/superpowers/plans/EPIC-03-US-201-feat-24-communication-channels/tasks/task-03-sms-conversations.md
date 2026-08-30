# Task 3 — SMS conversations

**Status:** not started
**Criteria:** `CC-11`, `CC-12`, `CC-13`
**Plan section:** [`implementation-plan.md#task-3--sms-conversations`](../implementation-plan.md#task-3--sms-conversations)
**Depends on:** Task 1 (ingestion), Task 2 (channel-generic reply path)

## Scope

Inbound SMS signature verification, `SmsWebhookController`, confirming the existing
`SmsNotificationChannelSender` covers the reply side with no channel-specific branch needed.

## When executed, record here

- Commit hash.
- Test command run and its actual output.
- Whether the configured SMS provider actually supports inbound webhooks (`A2`) — if not, record
  that the webhook controller was not deployed and only the reply path shipped, per the plan's
  Task 3 step 4.
