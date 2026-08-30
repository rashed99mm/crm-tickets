# Task 2 — WhatsApp channel

**Status:** not started
**Criteria:** `CC-6`, `CC-7`, `CC-8`, `CC-9`, `CC-10`
**Plan section:** [`implementation-plan.md#task-2--whatsapp-channel`](../implementation-plan.md#task-2--whatsapp-channel)
**Depends on:** Task 1

## Scope

`WhatsAppNotificationChannelSender`, `MetaSignatureVerifier`, `WhatsAppWebhookController` (anonymous,
`ExternalApi`), and the channel-generic ticket-reply path. Requires a `WhatsAppGateway`
`ExternalApiConfiguration` row (sandbox/mock credentials for testing — no production WhatsApp
Business account exists yet, see the spec's `A11`).

## When executed, record here

- Commit hash.
- Test command run and its actual output.
- Any deviation from the plan section above, and why.
- Confirmation the sandbox/mock provider was used, not a live credential.
