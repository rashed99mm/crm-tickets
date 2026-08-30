# EPIC-10 · Integrations

| | |
|---|---|
| **Epic** | `EPIC-10` |
| **Priority** | P1 |
| **Stories** | 0 specified — backlog only |

## Goal

Provide controlled integration with external systems *(rule specification §8)*.

## Status: not specified — and honestly thin

The brief's "APIs" under Integrations is ruled, in [`../../assessment/brief.md`](../../assessment/brief.md)
(ambiguities), as *this application's own HTTP API being fit for external consumption* — not
connectors to unspecified third parties. That reading is already delivered against: one response
envelope, stable system codes, truthful OpenAPI (`US-101`, `US-111`, `US-122`, `US-124`). What
remains is genuinely blocked:

- **ERP** — no named ERP and no integration contract exist; `OQ-9`, `DEP-7`.
- **Email/SMS/WhatsApp providers** — email is `DEP-1` (sprint 9); the others are deferred
  indefinitely with reasons.
- **External systems generally** — none named by any stakeholder to date.

Architectural principle inherited from rule specification §17.2: external systems communicate
through adapters at integration boundaries, never embedded in domain logic. Failure behaviour is an
explicit requirement, not an implementation detail: no silent data loss, recorded failures, retry
policy defined per adapter (rule spec `NFR-INT-001`; BRD §13).

## Reserved backlog (rule-file titles — unspecified by design)

US-081 Manage API Access · US-082 Integrate with ERP · US-083 Configure Email Provider · US-084
Configure SMS Provider · US-085 Configure WhatsApp Provider · US-086 Integrate External System
