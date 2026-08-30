# 2026-08-28 — Ticket-created in-app notification via domain events (FEAT-15 slice)

**Spec:** `../../specs/EPIC-02-US-016-ticket-created-notification-design.md` (approved 2026-08-28)
**Status:** planned — implementation not started

## Criteria delivered

| AC | Summary | Task | Status |
|---|---|---|---|
| AC-N1 | Gateway dispatch to linked customer user, TICKET_CREATED, ref in message | 01, 03 | planned |
| AC-N2 | Durable row targets customer user; InApp; Sent; ref in message | 04 | planned |
| AC-N3 | Live SignalR client receives NotificationReceived | 04 | planned |
| AC-N4 | Events dispatched once, after save, handler failure non-fatal | 02 | planned |
| AC-N5 | Unlinked customer → no notification, no throw | 01, 03, 04 | planned |
| AC-N6 | No events → no dispatch, no scope opened | 02 | planned |

## Gaps accepted (recorded, not hidden)

- `Infrastructure/Jobs/NotificationSender.cs:76-79` no-op `SendAsync` stub not addressed in this
  slice — routing it through the gateway would duplicate durable rows (the InApp sender always adds a
  fresh `Notification`). Tracked as a separate inherited defect.
- No "notify staff role group" or "notify the acting agent" behaviour (spec A1 / out of scope).

## Execution log

_(filled as the tasks complete, with observed test output)_
