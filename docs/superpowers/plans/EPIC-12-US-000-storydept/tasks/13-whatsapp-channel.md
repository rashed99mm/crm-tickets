# Task 13 — WhatsApp channel (out + webhook)

## Traceability
Epic:   docs/requirements/epics/EPIC-03-communication-channels.md
Stories: US-230–US-240 (WhatsApp slice — to be filed per delivery-plan row 16; file the two
         webhook/outbound stories this task implements if absent)
FEAT:   FEAT-24 — delivery-plan.md row 16
Spec:   docs/superpowers/specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md
Plan:   docs/superpowers/plans/EPIC-03-US-201-feat-24-communication-channels/

## Work
Outbound: `WhatsAppNotificationChannelSender` (existing HTTP-based). WhatsAppWebhookController already
uses `IngestInboundChannelMessageCommand` → shared ingestion → dedupe on `ProviderMessageId`. All three
channel senders (Email, SMS, WhatsApp) now return `ProviderMessageId` on success.

## Gate
- [x] `WhatsAppNotificationChannelSender` exists with `ProviderMessageId` in result.
- [x] WhatsApp webhook + ingestion already wired.
- [x] Backend build clean (`dotnet build CustomerSupport.slnx` succeeded 0 errors).
