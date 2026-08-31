# US-923 · Impact/Urgency → Derived Priority Matrix

| Field | Value |
|---|---|
| **Story** | `US-923` |
| **Epic** | [EPIC-02 Ticket Management](../epics/EPIC-02-ticket-management.md) |
| **Feature** | [`FEAT-32`](../delivery-plan.md) |
| **Layer** | Vertical (backend + frontend) |
| **Actor** | Agent |
| **Priority** | P1 |
| **Sprint** | 20 — ticket domain enrichment (slice 2) |
| **Estimate** | 8 points |
| **Status** | `not started` |

## Story

**As an agent**, **I want** to record a ticket's impact and urgency and have the system derive its
priority, **so that** identical incidents get identical priorities regardless of who files them.

## Business rules

- Priority is **matrix-only** (decision 2026-08-31): derived from the 3×3 impact/urgency matrix in
  the spec; direct priority input removed from forms and refused by the API.
- Existing tickets keep their stored priority until first reclassified (spec A1); customer-origin
  tickets default `Medium`/`Medium` → `Normal` (spec A2).
- A changed derived priority writes a `Reprioritized` history row.

## Acceptance criteria

Owned by the spec: `AC-923.1`…`AC-923.7` in
[`EPIC-02-US-922-ticket-domain-enrichment.md`](../../superpowers/specs/EPIC-02-US-922-ticket-domain-enrichment.md).

## SQL tables

`Tickets` — new nullable `Impact`, `Urgency`. **Breaking API change:** create contract drops
`priority` (spec A10).

## Status evidence

Not yet shipped. Status is set from what is committed and executed, never from what is planned.
