# EPIC-14 · Phase 2 — Real-life ticket workflow, BI layer and UX redesign

| | |
|---|---|
| **Epic** | `EPIC-14` |
| **Priority** | P0 (assessment + product definition) |
| **Stories** | `US-901`…`US-918` — see [`../user-stories/`](../user-stories/) |
| **Features** | `FEAT-28` (workflow state machine + domain enrichment) · `FEAT-29` (BI executive dashboard) · `FEAT-30` (UX redesign) |
| **Spec** | [`../../superpowers/specs/EPIC-05-US-218-phase2-bi-workflow.md`](../../superpowers/specs/EPIC-05-US-218-phase2-bi-workflow.md) |
| **Screens** | admin-app: dashboard, tickets queue/create/detail, customer detail, reports, kb-admin, portal-app: home/dashboard |
| **Status** | `in progress` |

## Goal

Turn the supplied **BI & Workflow Specification** into the source of truth for how tickets
actually move, add the BI layer that answers the KPI catalogue the spec defines, enrich the domain
model with the data those KPIs and the workflow need, and redesign every screen so the UI behaves
like a working tool instead of "dead HTML".

Three features carry it:

1. **`FEAT-28` — the full 8-state lifecycle (`New → Open → Assigned → In Progress → Waiting for
   Customer / Waiting for Internal Team → Resolved → Closed`) plus the domain enrichment it depends
   on**: `Team` entity, `FirstResponseAt`/`LastResponseAt`/`ResolvedAt`/`ClosedAt` lifecycle
   timestamps, org-chain wiring, and the escalation handoff owner.
2. **`FEAT-29` — BI executive dashboard**: the KPI catalogue rendered from data that exists, a
   shared standard date filter, and drill-down where the schema allows.
3. **`FEAT-30` — UX redesign**: queue, assignment/status flows, customer profile, agent dashboard,
   portal — made workflow-driven and real, preserving `AsyncState<T>`, i18n and RTL conventions.

## Why this is "Phase 2"

The brief's Phase 1 (the twelve areas S1–S9) is largely shipped. This epic is the document-driven
phase: the pasted BI & Workflow Specification defines lifecycles, business rules, SLA behaviour,
KPIs, filters, drill-down and an event model. Phase 2 means "the ticketing system behaves the way
the specification says a real support operation behaves" and "the BI layer answers the operation's
KPI catalogue" — strictly following that workflow.

## Domain decisions (recorded; rationale in the spec's Assumptions)

- **`Escalated` is a marker, not a 9th status.** The existing parallel `EscalationState`
  (`None/Warning/Level1/Level2/Level3`) carries escalation; this epic adds the missing **owner**
  (`Ticket.EscalationAssigneeId`) so an escalated ticket names the Supervisor/Specialist who holds it.
- **Both waiting states pause the SLA clock**, matching the spec's "time waiting on the customer —
  or an internal team — is not counted against the SLA" rule.
- **EPIC-13 (mockup fidelity) folds into this epic's presentation slice** (`FEAT-30`) so the visual
  layer and the workflow-driven redesign are one pass, not two that fight.
- **CSAT and real-time BI stay cut**, recorded in the spec with reasons (no rating-collection
  backend; reporting reads committed tables, not an event stream).

## Delivery slices

| Slice | Features | Scope | Criterion blocks |
|---|---|---|---|
| 0 | `FEAT-28` | Domain enrichment: `Team`, lifecycle timestamps, org-chain wiring, escalation owner | `AC-508`…`AC-512`, `AC-536` |
| 1 | `FEAT-28` | 8-state lifecycle: transition table, SLA pause on waiting states, reopen, UI status model | `AC-501`…`AC-507` |
| 2 | `FEAT-29` | BI: executive dashboard endpoint + screens, standard date filter, KPI catalogue, drill-down | `AC-513`…`AC-520` |
| 3 | `FEAT-30` | Agent UX: queue, detail (conversation-first, guided transitions), create (type-ahead) | `AC-521`…`AC-526` |
| 4 | `FEAT-30` | Dashboard, customer profile, reports bento, KB admin, portal | `AC-527`…`AC-535` |

## Dependencies and boundary

- `FEAT-28` depends on the shipped ticket aggregate, SLA scanner and conversation record
  (`FEAT-17`, `FEAT-14`).
- `FEAT-29` depends on the shipped report endpoints (`US-601…604` + addendum `US-606/607/610`) and
  on `FEAT-28`'s lifecycle timestamps replacing the A5/A7 approximations where the spec says.
- `FEAT-30` depends on the existing feature APIs and route contracts; missing data uses the
  non-interactive unavailable state, never fabricated values or an unplanned endpoint.
- Storydept tasks 16–21 (RTL/Arabic, branding, API-key, tenancy) continue in parallel — this epic
  is document-first.

## Definition of done

Spec approved → per feature: failing tests first → vertical backend+frontend ship → suite output
pasted → clean warnings-as-errors build → story `Status`/`Status evidence` updated from what was
executed → feature-complete commit. Highest-regression tests: lifecycle transitions, SLA
pause/resume, reopen, enrichment FK integrity.