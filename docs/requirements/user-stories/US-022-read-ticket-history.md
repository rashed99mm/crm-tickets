# US-022 · Read a ticket's history

| Field | Value |
|---|---|
| **Story** | `US-022` *(was `US-1.32`)* — rule proposal: *View Ticket History* |
| **Epic** | [EPIC-02 Ticket management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-08` Ticket history](../delivery-plan.md#feat-08--ticket-history) |
| **Layer** | Backend |
| **Ships with** | [US-128](./US-128-ticket-detail-with-guarded-actions.md) *(frontend)* |
| **Actor** | Support Agent |
| **Priority** | P1 |
| **Sprint** | [3 — Ticket detail, lifecycle, assignment and history](../delivery-plan.md#sprint-3--ticket-detail-lifecycle-assignment-and-history) · Slice S1 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.13 |
| **Spec criteria** | AC-50 |
| **Depends on** | [US-121](./US-121-every-change-recorded-immutably.md) |

## Story

**As an agent**, **I want** the history newest first with real names, **so that** I can see at a glance what just happened and who did it.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criteria:

- Entries return newest first with the actor's display name resolved for reading (from AC-50).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Newest first with display names (spec AC-50)

Given a ticket with history, when fetching it, then entries are returned newest first with the
actor's display name.

## SQL tables

`TicketHistory` read path joins `AspNetUsers.DisplayName` — from the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickethistory):

```sql
CREATE INDEX IX_TicketHistory_Ticket_Occurred
    ON [dbo].[TicketHistory] ([TicketId], [OccurredAtUtc] DESC);
-- ActorId resolved to [AspNetUsers].[DisplayName] at read time
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-50 | Api.IntegrationTests | PASS `AC50_TicketHistory_IsNewestFirstWithActorDisplayNames` | a ticket with several history rows / fetch detail / inspect entries | newest first, each carrying the actor's display name |
| TC-02 | AC-50 (resolution rule) | Application.Tests | PASS `AC50_HistoryRow_StoresActorIdNotName` — asserts the persisted row holds the id and no name column exists | history rows stored with actor ids / read / inspect output | names resolved for display; the stored row still holds only the id |

## Notes

Display names are resolved for reading; the stored actor is an id. Storing the name would freeze it, so a renamed user's past entries would disagree with their present name — and deactivating a user must not orphan history, which is `FR-10.11` in the proposed S9.

## Open questions

None.

## Status evidence

Served by `GetTicketByIdQuery` (built in FEAT-04, claimed here).

AC-50 -> `AC50_TicketHistory_IsNewestFirstWithActorDisplayNames` and
`AC50_HistoryRow_StoresActorIdNotName`.

The second is the criterion's design half: the row stores `ActorId` and the name is a **read-time
projection**. Denormalising the name would freeze a value that changes, inside an append-only table
that by construction can never be corrected.

**Carried forward:** name resolution is one lookup per distinct actor through
`IIdentityUserService`, because `ApplicationUser` sits outside `IRepository<T>`'s `BaseEntity`
constraint. Fine for a handful of rows; it would become an N+1 if history grew large.

Run 2026-08-26: 233 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
