# US-143 · Internal team notes

| Field | Value |
|---|---|
| **Story** | `US-143` |
| **Epic** | [EPIC-04 Agent workspace](../epics/EPIC-04-agent-workspace.md) |
| **Feature** | `FEAT-28` Agent workspace tasks |
| **Layer** | Backend + internal frontend only |
| **Actor** | Support Agent / Supervisor |
| **Priority** | P1 |
| **Estimate** | 2 points |
| **Status** | `done` |

## Story

**As a support agent**, **I want** to add internal notes on a ticket that only staff can see, **so that** I can share context with my team without exposing it to the customer.

## Acceptance criteria

#### AC-143.1 — Create internal note

Given a ticket detail view, when I enter text in the internal notes field and save, then a note is attached to the ticket and marked as internal.

#### AC-143.2 — Internal notes visible to staff only

Given a ticket with internal notes, when viewed by a staff agent, then the notes are visible. Given the same ticket viewed on the customer portal, then the notes are not present in the response.

#### AC-143.3 — Internal notes listed on ticket

Given a ticket with internal notes, when an agent opens the ticket, then the notes appear in a dedicated staff-only section with author and timestamp.

## SQL tables

```sql
TicketNotes(Id, TicketId, Body, IsInternal, CreatedBy, CreatedAt)
```

Note: `IsInternal=true` rows are NEVER exposed on the portal API surface.

## Test cases

| # | Criterion | Level | Test | Given / When / Then |
|---|---|---|---|---|
| TC-01 | AC-143.1 | Integration | `CreateNote_InternalFlag_Persists` | Given a note with IsInternal=true, when saved, then it is stored with that flag |
| TC-02 | AC-143.2 | Integration | `PortalApi_NeverReturnsInternalNotes` | Given internal notes on a ticket, when the portal API is called, then the notes array does not contain them |
| TC-03 | AC-143.3 | Component | `TicketDetail_ShowsInternalNotesSection` | Given a ticket with internal notes, when rendered by staff, then the notes section is visible with author and time |

## Open questions

None.

## Status evidence

Backend: `TicketNote` entity with `IsInternal` flag. Internal notes excluded from portal DTOs.
Frontend: internal notes section on ticket detail in admin-app only.
