# US-925 · Related / Duplicate Ticket Links

| Field | Value |
|---|---|
| **Story** | `US-925` |
| **Epic** | [EPIC-02 Ticket Management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-32`](../delivery-plan.md) |
| **Layer** | Vertical (backend + frontend) |
| **Actor** | Agent |
| **Priority** | P2 |
| **Sprint** | 20 — ticket domain enrichment (slice 4) |
| **Estimate** | 8 points |
| **Status** | `not started` |

## Story

**As an agent**, **I want** to link a ticket to another as related or as a duplicate, **so that**
duplicate work is visible and connected incidents can be traversed instead of cited in comments.

## Business rules

- `RelatedTo` (symmetric display) and `DuplicateOf` (directional); single stored row; self-links,
  unknown targets, duplicate rows and the direct two-ticket duplicate cycle refused (spec A7).
- Resolving with code `Duplicate` requires an existing `DuplicateOf` link (409 otherwise); creating
  a link never auto-resolves (spec A8).
- No merge — a link is a pointer (out of scope).

## Acceptance criteria

Owned by the spec: `AC-925.1`…`AC-925.5` in
[`EPIC-02-US-922-ticket-domain-enrichment.md`](../../superpowers/specs/EPIC-02-US-922-ticket-domain-enrichment.md).

## SQL tables

New `TicketLinks` (FKs source/target → `Tickets`, unique `(SourceTicketId, TargetTicketId, LinkType)`).

## Status evidence

Not yet shipped. Status is set from what is committed and executed, never from what is planned.
**Cut first** if the sprint runs out of time.
