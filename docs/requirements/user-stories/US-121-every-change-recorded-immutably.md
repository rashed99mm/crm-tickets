# US-121 · Every change is recorded and nothing can rewrite it

| Field | Value |
|---|---|
| **Story** | `US-121` *(was `US-1.31`)* |
| **Epic** | [EPIC-02 Ticket management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-08` Ticket history](../delivery-plan.md#feat-08--ticket-history) |
| **Layer** | Backend |
| **Ships with** | [US-128](./US-128-ticket-detail-with-guarded-actions.md) *(frontend)* |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Support Manager |
| **Priority** | P0 |
| **Sprint** | [3 — Ticket detail, lifecycle, assignment and history](../delivery-plan.md#sprint-3--ticket-detail-lifecycle-assignment-and-history) · Slice S1 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-2.13, BR-5, NFR-9 |
| **Spec criteria** | AC-48, AC-49 |
| **Depends on** | [US-016](./US-016-move-along-the-lifecycle.md), [US-014](./US-014-supervisor-assigns-work.md) |

## Story

**As a support manager**, **I want** an immutable record of every change to a ticket, **so that** a dispute about what happened can be settled from the system.

## Business rules

- BR-5 — history append-only; nothing updates/deletes an entry or exposes an operation that could
  (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — Every change appends history (spec AC-48)

Given a ticket is created, assigned, reassigned, or has its status changed, then a history row is
appended recording actor, UTC timestamp, the change type, and the from/to values.

#### AC2 — History append-only by construction (spec AC-49)

History is append-only. No endpoint updates or deletes a history row, and **none is exposed that
could**.

## SQL tables

`TicketHistory` — the one append-only table; full definition in the
[S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md#tickethistory):

```sql
CREATE TABLE [dbo].[TicketHistory] (
    [Id]            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [TicketId]      UNIQUEIDENTIFIER NOT NULL REFERENCES [dbo].[Tickets] ([Id]),
    [ActorId]       NVARCHAR(450)    NOT NULL REFERENCES [dbo].[AspNetUsers] ([Id]),
    [ChangeType]    NVARCHAR(32)     NOT NULL,
    [FromValue]     NVARCHAR(64)     NULL,  [ToValue] NVARCHAR(64) NULL,
    [OccurredAtUtc] DATETIMEOFFSET   NOT NULL
);
-- No UPDATE or DELETE path exists for this table anywhere in the code.
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-48 | Application.Tests | PASS `AC48_EveryTicketEvent_PersistsItsOwnHistoryRow` — all five change types in one timeline, in order, each with actor and UTC timestamp | ticket created / assigned / reassigned / status changed (faked ports) / execute handler / inspect appended rows | row per event with actor, UTC timestamp, change type, from/to |
| TC-02 | AC-49 | Api.IntegrationTests | PASS `AC49_NoEndpointExposesHistoryMutation` — enumerates `EndpointDataSource` and asserts no history route carries a mutating verb. Plus `AC49_UpdatingAHistoryRow_IsRefused` / `AC49_DeletingAHistoryRow_IsRefused` at the persistence layer | enumerate every registered route / inspect methods and paths | no endpoint mutates history — PUT/PATCH/DELETE absent by construction |

## Notes

The second criterion is a statement about the API surface, not about an implementation. It is satisfied by an absence, which makes it easy to violate later by adding a well-meaning correction endpoint — so it is written down as a criterion rather than assumed.

This story comes after assignment because assignment events are among the things it records.

## Open questions

None.

## Status evidence

The aggregate appends its own rows; enforcement is the `SaveChanges` guard from
[ADR-0010](../../adr/0010-append-only-history-enforced-by-a-savechanges-guard.md).

AC-48 -> `AC48_EveryTicketEvent_PersistsItsOwnHistoryRow` - one ticket taken through create, assign,
reassign, status change, resolve and reopen, asserting the exact six-row timeline in order. AC-49 ->
`AC49_UpdatingAHistoryRow_IsRefused`, `AC49_DeletingAHistoryRow_IsRefused` and
`AC49_NoEndpointExposesHistoryMutation` (the surface audit TC-02 asked for).

**This settles a Phase 0 debt.** ADR-0010 argued the guard beats absent columns partly *because it
is testable*; that had been an assertion for three phases and is now a fact.

**Qualification, recorded rather than glossed:** during FEAT-06 the guard produced a *false*
refusal. A client-assigned `Id` made EF mark a brand-new appended row `Modified`, and the guard
correctly refused it - breaking every status change with a 500. The guard cannot distinguish a
mis-tracked insert from a genuine mutation, and ADR-0010 did not anticipate that.

Run 2026-08-26: 233 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
