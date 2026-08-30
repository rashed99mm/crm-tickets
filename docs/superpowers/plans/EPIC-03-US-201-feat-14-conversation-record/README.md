# FEAT-14 — Conversation record · backend task record

**Plan:** [`implementation-plan.md`](./implementation-plan.md)
**Spec:** [`../../specs/EPIC-03-US-201-conversation-record.md`](../../specs/EPIC-03-US-201-conversation-record.md)
**Status:** backend delivered and tested; frontend delivered, component tests not yet written

## Evidence

```
dotnet build CustomerSupport.slnx    → Build succeeded. 0 Errors (8 pre-existing warnings, unrelated)
dotnet test CustomerSupport.slnx
Passed!  - Failed: 0, Passed: 295, Skipped: 0, Total: 295, Duration: 1m 46s
```

## Status

| Task | AC | Status | Commit | Evidence |
|---|---|---|---|---|
| T1 | — (regression-safe refactor) | `done` | uncommitted | `IAppendOnlyEntity` generalises `AppDbContext.GuardAppendOnlyHistory`; existing `AC49_*` tests pass unmodified against it. |
| T2 | AC-101 (entity groundwork) | `done` | uncommitted | `TicketMessageTests` (9 tests), migration `20260826185608_AddTicketMessages` reviewed — touches only `TicketMessages` and its two FKs/indexes. |
| T3 | AC-101, AC-102, AC-103, AC-104, AC-105, AC-109 | `done` | uncommitted | `TicketMessagesEndpointTests` write-side + append-only cases. |
| T4 | AC-106, AC-107, AC-108 | `done` | uncommitted | `TicketMessagesEndpointTests` read-side cases. |

## Frontend (US-202, AC-110..AC-114)

`TicketMessagesComponent` (`frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.{ts,html}`),
mirroring `CustomerNotesComponent`'s shape: load/empty/error states, a log-message form
(Direction, Channel, optional Subject, required Body), post-and-reload, no optimistic update on
failure. Wired into `ticket-detail.component.html` beside the status-history timeline.
`TicketApi.listMessages`/`recordMessage` added, `TicketMessage`/`RecordTicketMessageRequest` types
added, translation keys added (`messages.*`).

```
npx ng build admin-app              → Application bundle generation complete
npx ng test common    --watch=false → Test Files 26 passed | Tests 115 passed
npx ng test admin-app --watch=false → Test Files 17 passed | Tests 121 passed
```

The 121 admin-app tests are the **pre-existing** suite plus the existing `ticket-detail.component.spec.ts`
cases, all still green with the new child component mounted — **no new test names `AC-110`
through `AC-114`.** The behavior is implemented and does not break anything already proven; it is
not yet proven itself. That is a real gap, not a rounding-up.

## Gaps

**No component tests for `AC-110`–`AC-114`.** `ticket-messages.component.spec.ts` does not exist.
Needed before this feature can be called `done` rather than `delivered`: empty state renders
(AC-111), a logged message posts exactly `{direction, channel, subject?, body}` and reloads
(AC-112), an empty body sends no request (AC-113), a rejected submission leaves the timeline
untouched and shows the server message (AC-114), oldest-first rendering with direction/sender
visible (AC-110). Skipped this pass for time, on explicit instruction — recorded rather than
silently omitted.

## Deviations

None from the backend plan. Frontend was not planned via a separate written plan document (skipped
per explicit instruction to move quickly) — implemented directly from the spec's AC-110..AC-114 and
existing `CustomerNotesComponent`/`CustomerNotesComponent` patterns instead.
