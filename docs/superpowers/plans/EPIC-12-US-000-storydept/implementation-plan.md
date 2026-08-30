# Sprint storydept — close all 12 core-feature gaps end-to-end

> Created 2026-08-27. Decisions locked: AI = OpenRouter (key in user-secrets ONLY, rotate after
> the session), free model `meta-llama/llama-3.3-70b-instruct:free`; ERP deferred (the gateway
> port is the future plug point); tenancy / multi-branch LAST. Non-goal: real ERP integration.

## Traceability rule

Every task file opens with a Traceability block: Epic (`docs/requirements/epics/EPIC-nn-*.md`),
Stories (`docs/requirements/user-stories/US-nnn-*.md`), FEAT-nn (delivery-plan.md row), and any
existing spec/plan folder under `docs/superpowers/`. A task whose scope has no story files yet
SAYS so and files the missing story as its first step — never silently.

## Verified baseline (2026-08-27)

- Green (run this session): portal 52/52 · admin 183/183 · permission 12/12 · backend build 0 errors.
- Red (~29): PortalJourney ×8, PortalRegister ×2, OTP ×3, ContentFaq ×2, WhatsApp ×9, ContractHardening ×1.

## Invariants (violating any of these already cost hours this session)

1. Editing `projects/common` ⇒ MUST `npx ng build common` before any app test (apps' tests consume `dist/common`).
2. Vitest isolation only works with `configureTestingModule` INSIDE each test, `TestBed.resetTestingModule()` first.
3. `HttpTestingController`: string `expectOne` matches url WITH query — use function matchers on `r.url`.
4. DbContext has `EnableRetryOnFailure` ⇒ user transactions ONLY inside
   `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`.
5. Shared integration fixtures ⇒ `db.ChangeTracker.Clear()` before restoring seeded state.
6. Every new error code wired in THREE places: `SystemCode.cs`, `SystemCodeMap.cs`, `Resources.yaml`
   (`EveryErrorCode_HasABilingualMessage` scans this).
7. OpenRouter key: user-secrets/env only — never appsettings.json, never git.

## Sequencing

S0 (01–04) → S1 (05–10) → S2 (11–14) → S3 (15) → S4 (16–19) → S5 (20) → S6 (21).

Per task: failing test first, then implementation, gate = named tests green with output pasted.
Per workstream: full build + filtered suites. Never claim a pass without running it.

## Task index

| # | Task | Epic | FEAT |
|---|---|---|---|
| 01 | Fix portal journey backend | EPIC-07 | FEAT-22 |
| 02 | Fix KB FAQ endpoint | EPIC-06 | FEAT-18 |
| 03 | Fix OTP handler | EPIC-07 | — |
| 04 | Contract hardening sweep | EPIC-09 | FEAT-09 |
| 05 | ContentDto completion | EPIC-06 | FEAT-18 |
| 06 | KB admin UI | EPIC-06 | FEAT-18 |
| 07 | Portal journey UI wiring | EPIC-07 | FEAT-22 |
| 08 | SLA notifications | EPIC-05 | FEAT-17 |
| 09 | Agent workspace (tasks/quick replies/team notes) | EPIC-04 | new |
| 10 | CSAT report | EPIC-08 | FEAT-20 |
| 11 | Notification gateway core | EPIC-03 | FEAT-15 |
| 12 | Email channel | EPIC-03 | FEAT-15/24 |
| 13 | WhatsApp channel | EPIC-03 | FEAT-24 |
| 14 | SMS + channels through gateway | EPIC-03 | FEAT-25/26/27 |
| 15 | OpenRouter AI provider | EPIC-11 | FEAT-21 |
| 16 | Responsive + RTL + Arabic copy | EPIC-12 | FEAT-23 |
| 17 | Custom branding | EPIC-12 | FEAT-23 |
| 18 | API-key auth on ExternalApi | EPIC-10 | new |
| 19 | E2E journey (Playwright) | EPIC-02/13 | FEAT-11 |
| 20 | Branches / tenancy | EPIC-12 | FEAT-16 |
| 21 | Closeout + traceability sync | all | all |
