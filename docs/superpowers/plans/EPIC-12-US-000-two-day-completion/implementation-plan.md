# Two-Day Completion Plan (sequencing) — rewritten with real-code evidence

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Date:** 2026-08-25 · **Budget:** ~2 days · **Covers:** `FEAT-03`..`FEAT-11`.

**Nature of this document:** it is a *sequencing* plan — it orders already-shipped feature loops against the clock; it contains no feature's implementation itself. The real code for every feature it schedules now lives in that feature's own dedicated plan, each rewritten with real code in this same pass:
`EPIC-02-US-001-feat-03-customer-records/`, `-feat-04-ticket-capture/` (+`-frontend/`), `-feat-05-ticket-queue/` (+`-frontend/`), `EPIC-02-US-016-feat-06-ticket-lifecycle/` (+`-frontend/`), `-feat-07-assignment-authorization/`, `-feat-08-ticket-history/`, `EPIC-06-US-504-feat-11-knowledge-base/`. This rewrite therefore *cites the real shipped code* as evidence each scheduled block landed, rather than re-deriving it.

## Global constraints

- Each feature still shipped under the SDD loop: backend plan → backend TDD → frontend plan → frontend TDD → tests green → commit.
- Integration tests run against LocalDB (the `CrmApiFactory` per-run database), never InMemory — `AC-9`/`AC-41` need a real unique index and `rowversion`.
- The cut (FEAT-12 notes, FEAT-13 attachments) is recorded as a decision, not a forgotten feature.

## Task 1 — Phase 0: domain + schema (done before this plan)

**Evidence (real code):**
- `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` — `Status`/`AssigneeId` private setters, `ChangeStatus`/`AssignTo`/`IsAssignedTo`, `TicketStatus.CanTransitionTo`.
- `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs` — `GuardAppendOnlyHistory()` enforces `IAppendOnlyEntity` (ADR-0010, `AC-49`).
- `20260825170424_AddTicketWorkflow` migration — 7 tables, 16 indexes, `TicketReferenceSequence`.

Seven EF configurations, filtered unique indexes, `rowversion`, no cascades. This is the substrate Tasks 2–4 build on.

- [ ] **Step 2: Run:** `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketTests"`
Expected: PASS — 50 domain tests, part of ~156 passing at the time.

- [ ] **Step 3: Commit:** (shipped) `git commit -m "feat(schema): ticket workflow domain + migration (Phase 0)"`

## Task 2 — Phase 1: Customers + ticket capture (`FEAT-03`, `FEAT-04`)

**Evidence (real code):**
- `Customer.Create` / `CustomersController` / `CreateCustomerCommandHandler` — duplicate email → 409 via `IDbExceptionTranslator` (`AC-9`). See `EPIC-02-US-001-feat-03-customer-records/implementation-plan.md`.
- `Ticket.Create` + `CreateTicketCommandHandler` — reference issued, status `New`, field-keyed 400 (`AC-29`..`AC-31`); `CategorySeeder` seeds the fixed four buckets (assumption `A4`). See `EPIC-02-US-016-feat-04-ticket-capture/implementation-plan.md`.
- `ticket-create.component.ts` — client rules mirror server's, `errors[]` land on the control (`AC-59`,`AC-60`). See `EPIC-02-US-016-feat-04-ticket-capture-frontend/`.

**Gate (as shipped):** an agent creates a ticket through the browser; a server-side field error appears under its own control.

- [ ] **Step 2: Run:** `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CustomerEndpointTests|FullyQualifiedName~CreateTicketEndpointTests"`
Expected: PASS.

- [ ] **Step 3: Commit:** (shipped) per-feature commits in the two feature plans above.

## Task 3 — Phase 2: The queue (`FEAT-05`)

**Evidence (real code):**
- `GetTicketsQueryHandler` — `PredicateBuilder` combinable filters, newest-first (`AC-32`,`AC-33`); `mine` from token (`AC-34`); `unassigned` flag (`AC-82`). See `EPIC-02-US-016-feat-05-ticket-queue/`.
- `ticket-queue.component.ts` — `AsyncState` union; loading/empty/error distinct (`AC-58`). See `EPIC-02-US-016-feat-05-ticket-queue-frontend/`.

- [ ] **Step 2: Run:** `cd frontend && npx ng test admin-app --watch=false --filter ticket-queue`
Expected: PASS.

- [ ] **Step 3: Commit:** (shipped) per-feature commits above.

## Task 4 — Phase 3: Detail, lifecycle, assignment, history (`FEAT-06`, `FEAT-07`, `FEAT-08`)

**Evidence (real code, referenced not restated):**
- `GetTicketById` + `TicketsController.GetById` — customer summary + history newest-first (`AC-35`,`AC-36`,`AC-50`).
- `ChangeTicketStatusCommandHandler` — refusal → 409 `ERR021`/`ERR022` (`AC-37`..`AC-39`); `DbUpdateConcurrencyException` → 409 `ERR024` (`AC-41`).
- `AssignTicketCommandHandler` — `Supervisor` policy; agent assigning anything → 403; in-handler ownership check (`AC-42`..`AC-47`).
- `TicketHistory` append-only, guarded by `AppDbContext.GuardAppendOnlyHistory()` (`AC-49`, `BASE-14`).

Plans: `EPIC-02-US-016-feat-06-ticket-lifecycle/`, `-feat-07-assignment-authorization/`, `-feat-08-ticket-history/`.

- [ ] **Step 2: Run:** `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketLifecycle|FullyQualifiedName~Assignment"`
Expected: PASS.

- [ ] **Step 3: Commit:** (shipped) per-feature commits.

## Task 5 — Phase 4: Hardening, localisation, journey (`FEAT-09`, `FEAT-10`, `FEAT-11`)

**Evidence:**
- Envelope sweep: every endpoint returns one `code` per condition (`AC-51`,`AC-54`,`AC-66`); 500 → `ERR900` + `traceId`, no stack/SQL in body (`AC-52`,`AC-53`). Implemented in `ResponseExtensions` + `WebApplicationExtensions` exception middleware.
- Bilingual: `LocaleStore` + `translate.pipe`; `dir` follows locale; language switch triggers no refetch (`AC-63`).
- The single Playwright journey: sign in → create → assign → change status → reload → history persisted (`AC-64`).
- Knowledge base (FEAT-11): `EPIC-06-US-504-feat-11-knowledge-base/` (published-only external host, versioning, FAQ, linking, Arabic search, view/vote).

- [ ] **Step 2: Run:** `cd backend && dotnet test CustomerSupport.slnx` then `cd frontend && npx ng test admin-app --watch=false`
Expected: full suites green; Playwright `US-129` journey green.

- [ ] **Step 3: Commit:** (shipped) hardening + knowledge-base commits.

## Task 6 — The cut (recorded decision)

`FEAT-12` (customer notes) and `FEAT-13` (customer attachments) were cut. The entities/tables survive (`CustomerNote`, `CustomerAttachment`, `Asset` exist in `AppDbContext`), with `AC-19`/`AC-25` defences unit-tested, but the API/UI features are cut, not the schema. Recorded in `rubric-traceability.md` under Scope cuts.

## Self-review

This document is a sequence map. Every block it schedules is evidenced by real, committed code in the linked feature plans; nothing here re-implements a feature. **No discrepancy** between the sequencing prose and the shipped code was found beyond the normal later correction that `FEAT-06`..`FEAT-08` landed as their own dedicated plans (already accounted for in `INDEX.md`).
