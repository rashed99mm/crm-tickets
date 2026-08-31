# US-922 · Resolution Discipline

| Field | Value |
|---|---|
| **Story** | `US-922` |
| **Epic** | [EPIC-02 Ticket Management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-32`](../delivery-plan.md) |
| **Layer** | Vertical (backend + frontend) |
| **Actor** | Agent |
| **Priority** | P1 |
| **Sprint** | 20 — ticket domain enrichment (slice 1) |
| **Estimate** | 8 points |
| **Status** | `not started` |

## Story

**As an agent**, **I want** resolving a ticket to require a resolution code and notes, and reopening
to be counted, **so that** every resolution carries an auditable record of how it was resolved.

## Business rules

- `Resolved` requires `ResolutionCode` (`Fixed | Workaround | Duplicate | CannotReproduce |
  NoResponse`) + `ResolutionNotes` (≤ 2000 chars) — enforced by validator (400, field errors) and
  aggregate guard (409) both.
- Reopen clears both fields and increments `ReopenCount`; no reopen cap (spec A4).

## Acceptance criteria

Owned by the spec: `AC-922.1`…`AC-922.7` in
[`EPIC-02-US-922-ticket-domain-enrichment.md`](../../superpowers/specs/EPIC-02-US-922-ticket-domain-enrichment.md).

## SQL tables

`Tickets` — new nullable `ResolutionCode`, `ResolutionNotes`; `ReopenCount` default 0.

## Status evidence

Not yet shipped. Status is set from what is committed and executed, never from what is planned.
