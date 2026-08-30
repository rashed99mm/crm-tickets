# Task 14 — SMS channel + all channels through one conversation record

## Traceability
Epic:   docs/requirements/epics/EPIC-03-communication-channels.md
Stories: US-230–US-240 SMS/LiveChat/WebForm slices
FEAT:   FEAT-25/26/27 — delivery-plan.md row 16
Spec:   docs/superpowers/specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md

## Work
1. SMS sender behind the same gateway adapter port (existing `SmsNotificationChannelSender`).
2. Live-chat widget and web form submissions already route through `IngestInboundChannelMessageCommand`
   → shared ingestion → `NotificationDelivery` logged via gateway.

`MESSAGE_CHANNELS` already enumerates WhatsApp/SMS/WebForm/LiveChat. All channels fan through
the same gateway dispatcher.

## Gate
- [x] All three channel senders (Email, SMS, WhatsApp) wired to `NotificationGateway` dispatcher.
- [x] Backend build clean.
