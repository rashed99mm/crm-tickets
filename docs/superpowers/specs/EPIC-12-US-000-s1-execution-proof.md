# S1 Execution Proof — Contract Hardening + the End-to-End Journey

**Date:** 2026-08-26
**Status:** Approved (user "go" 2026-08-26)
**Type:** Execution/proof epic over shipped features (FEAT-09 + FEAT-11)
**Slices:** S1

## Context

Every S1 feature from FEAT-02 through FEAT-08 and FEAT-12/13 is implemented: backend handlers,
screens, component tests, and the 270-test backend suite. What remains unproven is the
cross-cutting layer that only becomes demonstrable once the surface exists:

- **FEAT-09 contract hardening** — the envelope on every response, one documented code per named
  condition, failures that leak nothing, ISO/camelCase wire format. The delivery plan is explicit:
  these are continuous obligations proven by a dedicated pass.
- **FEAT-11 end-to-end journey** — `AC-64`'s single browser journey. `frontend/e2e/journey.spec.ts`
  exists but carries an honest header: **NOT YET RUN**. CLAUDE.md's commands table records
  "E2E: (none yet)". No E2E evidence of any kind has ever been pasted.

The proof pass also surfaced a **spec-level correction** recorded here as an assumption.

## Assumptions

- **A1 — Envelope message shape changed; stories are stale, code is authoritative.**
  US-122 describes the envelope as carrying `message: { ar, en }` (both languages in every body).
  The shipped contract — built and verified during the 2026-08-26 refactor sprint — carries a plain
  localized `message: string`, resolved server-side from `Accept-Language` (defaulting to Arabic),
  with both languages still present *in the catalogue* rather than in each body. Every new test
  asserts the **shipped** shape. This story file is corrected under Phase 3, not silently.
- **A2 — Conditions without endpoints get built.** AC-66 names oversized upload (413) and
  disallowed type (415). If no attachment endpoint enforces them yet, the smallest honest
  implementation (customer attachment upload limits + content-type allowlist per FEAT-13's story)
  is added with its own failing test first. Faking a pass or marking the condition N/A is not done.
- **A3 — The journey may edit the app.** Where `journey.spec.ts` expects an anchor the templates do
  not have (`data-testid`s), the template gains the anchor. Assertions are never weakened to make a
  selector match; production behaviour is never changed solely to please the test.
- **A4 — Admin account stands in for Supervisor.** The assign gate is supervisor-only; the seeded
  `admin@cce-platform.com` is assumed to clear it. If it does not, the journey records that finding
  and uses the real seed arrangement rather than loosening authorization.

## Acceptance criteria (cited from the S1 spec)

- **AC-E1** *(US-122 / spec AC-51)* — An integration test exercises every internal endpoint once
  successfully and asserts the exact shipped envelope keys:
  `{ success, code, message, data, errors, traceId, timestamp }`. A validation failure carries
  top-level `VAL001` plus one `errors[]` entry per invalid field.
- **AC-E2** *(US-122 / spec AC-66)* — Each named condition answers with its documented system code:
  duplicate email, delete guard, invalid transition, self-transition, concurrency conflict,
  ownership refusal, oversized upload (413), disallowed type (415).
- **AC-E3** *(US-123 leftovers / spec AC-52, AC-53)* — A forced unhandled exception returns 500 with
  a generic message and code; the body contains no stack trace, SQL text or connection string;
  every response carries `traceId`.
- **AC-E4** *(US-124 leftover / spec AC-54)* — A date-bearing DTO serializes ISO 8601 UTC on the wire.
- **AC-E5** *(US-129 / spec AC-64)* — One browser journey: sign in → create customer → create ticket →
  assign → change status → reload → the status change and its history persisted.

## Out of scope

FEAT-10 localisation sweep; G-6/G-7 product gaps; roadmap sprints 6–15; any new feature work beyond
A2's minimal 413/415 enforcement.
