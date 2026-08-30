# Plan: S1 Execution Proof — FEAT-09 contract pass + FEAT-11 journey

**Spec:** `docs/superpowers/specs/EPIC-12-US-000-s1-execution-proof.md`
**Date:** 2026-08-26
**Approach:** Run first, fill only verified gaps TDD-first, then drive the browser journey.

## Discovery that shaped this plan

A survey of `CustomerSupport.Tests` found FEAT-09 substantially more proven than the story files
claim (several read `partial`/`planned` but tests exist):

| Concern | Already proven by |
|---|---|
| Envelope on every endpoint (AC-51) | `Integration/ContractHardeningTests.cs::AC51_EveryEndpoint_AnswersInTheEnvelope`, `AC51_EveryFailure_CarriesACodeAndBothLanguages` |
| Leak-free failures (AC-52) | same file :: `AC52_AFailingRequest_LeaksNoInternals`, unknown-route + malformed-JSON tests |
| TraceId (AC-53) | same file :: `AC53_Responses_CarryATraceIdentifier` |
| camelCase + ISO dates (AC-54) | same file :: both `AC54_*` tests |
| Duplicate email / delete guard / invalid+self transition / concurrency codes (AC-66) | Customer/Ticket/Lifecycle endpoint tests (ERR008/009/013/014/015) |
| 413 + 415 (AC-66) | `CustomerAttachmentEndpointTests.cs` ERR042@136, ERR043@162 |
| VAL001 field-keyed validation | `CustomerNotesEndpointTests.cs:103` |

Verified gaps this plan closes:
1. **ERR016 ownership refusal** (AC-66 "ownership refusal") — no integration test asserts it end-to-end.
2. **Forced unhandled exception → 500 generic** (AC-53 second half) — middleware arm exists
   (`ExceptionHandlingMiddleware` `_` case), no test drives it.
3. **The E2E journey has never been executed.**

## Tasks

### Phase 1 — backend proof (each task fails first where a gap is real)

1. Run `dotnet test CustomerSupport.slnx`; paste output. Confirm the existing contract suite is
   green as surveyed. *(Execution evidence, no new code.)*
2. Write failing `ContractHardeningTests` addition: a supervisor-owned ticket assigned to agent A,
   agent B calls status change → expect 403 envelope `ERR016`. *(AC-E2)*
   - If the server currently answers otherwise, fix the handler/mapping — never the assertion.
3. Write failing test: an endpoint whose handler throws `InvalidOperationException` → expect HTTP 500,
   code `Internal`/generic per shipped mapping, body containing no exception text; traceId present.
   Driven via a test-only diagnostic route or a seeded failure hook consistent with existing harness
   conventions — if neither exists without touching production behavior, record the deviation rather
   than shipping test-only seams into production code. *(AC-E3)*
4. Re-run full suite; paste output.

### Phase 2 — FEAT-11 journey

5. Audit every selector in `frontend/e2e/journey.spec.ts` against current templates
   (`login.component.html`, `customer-create.component.html`, `ticket-create.component.html`,
   `ticket-detail.component.html`). Fix mismatches by adding `data-testid`s to templates.
6. Start InternalApi on :5074 (user-secrets config already proven), run `npx playwright test`,
   iterate until green. Paste real output. Update CLAUDE.md commands table E2E row.
7. Update story files' Status-evidence sections *with the pasted results*.

### Phase 3 — documentation reconciliation

8. US-122: correct stale `{ar,en}` envelope description to the shipped localized-string contract;
   fill TC rows with actual test names/status; flip status.
9. US-123/US-124: fill outstanding TC rows with real evidence discovered above.
10. US-129: TC-01 `planned` → executed + evidence; status `done` if AC-E5 holds.
11. `delivery-plan.md`: sprint 2–5 status columns reflect reality; note the amended baseline.
12. `rubric-traceability.md`: refresh standing-gap paragraph (FEAT-03–08 + 12/13 shipped; contract
    hardening proven; journey executed); update Testing row.

## Ship gate

Green backend suite (pasted), Playwright journey passing visibly (pasted), docs reconciled after
that verification — never before it.
