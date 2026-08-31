# US-924 · Ticket Tags

| Field | Value |
|---|---|
| **Story** | `US-924` |
| **Epic** | [EPIC-02 Ticket Management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-32`](../delivery-plan.md) |
| **Layer** | Vertical (backend + frontend) |
| **Actor** | Agent |
| **Priority** | P2 |
| **Sprint** | 20 — ticket domain enrichment (slice 3) |
| **Estimate** | 5 points |
| **Status** | `not started` |

## Story

**As an agent**, **I want** to tag tickets with free-form labels and filter the queue by tag,
**so that** ad-hoc groupings (`billing`, `vip`, …) don't require a taxonomy change.

## Business rules

- Normalization per spec A6: trim, collapse whitespace, invariant lowercase; 1–30 chars; Unicode
  letters (Arabic included), digits, dash, space; max 10 per ticket; duplicates refused.
- Adds/removes write `TagAdded`/`TagRemoved` history rows.
- Queue filter `tag=` is server-side and composes with existing filters.

## Acceptance criteria

Owned by the spec: `AC-924.1`…`AC-924.5` in
[`EPIC-02-US-922-ticket-domain-enrichment.md`](../../superpowers/specs/EPIC-02-US-922-ticket-domain-enrichment.md).

## SQL tables

New `TicketTags` (FK `Tickets`, unique `(TicketId, Value)`).

## Status evidence

Not yet shipped. Status is set from what is committed and executed, never from what is planned.
Cut order: cut after US-925, before US-922/US-923.
