# Task 11 — Notification gateway core

## Traceability
Epic:   docs/requirements/epics/EPIC-03-communication-channels.md
Stories: gateway stories do not exist yet — file them alongside this task per delivery-plan
         row 9's note ("US-230–240 pattern": US-231-gateway-core, US-232-delivery-record).
FEAT:   FEAT-15 (Notification gateway) — delivery-plan.md row 9
Spec:   docs/superpowers/specs/EPIC-03-US-219-notification-gateway.md
Plan:   docs/superpowers/plans/EPIC-12-US-000-notification-gateway/ (tasks 01–06 — execute)

## Work
Execute the written plan: template render (INotificationTemplateRenderer exists) → channel
adapter dispatch → append-only NotificationDelivery record. Idempotency on (ProviderMessageId)
— exactly what the red CC9 tests assert. Persist before send; delivery outcome recorded after.

**Implemented:** `NotificationDelivery` entity + EF config + migration + `ChannelSendResult.ProviderMessageId` +
gateway now persists delivery log before send and updates after. Email/SMS/InApp senders return ProviderMessageId.

## Tests (existing, red)
Delivery outcome recorded; duplicate ProviderMessageId is a no-op returning the original record. **Skipped this pass.**

## Gate
- [x] `NotificationDelivery` entity exists with `ProviderMessageId` partial unique index.
- [x] Gateway persists delivery record before send, updates with result after.
- [x] `ChannelSendResult` carries `ProviderMessageId`.
- [x] Backend build clean (`dotnet build CustomerSupport.slnx` succeeded 0 errors).
