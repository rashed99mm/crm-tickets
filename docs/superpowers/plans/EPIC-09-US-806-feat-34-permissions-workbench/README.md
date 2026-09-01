# FEAT-34 — Role & permission workbench · record

**Epic:** `EPIC-09` · **Stories:** `US-806`, `US-807` ·
**Spec:** [`EPIC-09-US-806-permissions-workbench-and-global-confirmation.md`](../../specs/EPIC-09-US-806-permissions-workbench-and-global-confirmation.md)

**Status: implemented (frontend fully verified; backend unit-tested, integration verification
BLOCKED by a pre-existing environment defect).** Spec approved 2026-09-01; backend and frontend
plans and all twelve task files written the same day; implementation executed the same day.

## What shipped

| Layer | Criteria | Status |
|---|---|---|
| Backend — atomic `PUT /api/admin/permissions/{roleId}` | `AC-806.1`…`AC-806.10` | **Code complete, unit-tested (13/13 pass). Integration-verified: BLOCKED** — see below. |
| Frontend — confirmation queue + dialog hardening | `AC-807.1`…`AC-807.7` | **Implemented and verified** — 37 tests pass across `common` and `admin-app`. |
| Frontend — staged workbench (draft, save, failures, discard/refresh/guard, search/grouping/bulk, a11y/RTL) | `AC-806.11`…`AC-806.26` | **Implemented and verified** — 31 tests pass. |

## The backend integration-test blocker

Three separate `dotnet test` runs of `Integration/PermissionTests.cs` failed completely (20/20,
1/1, 20/20), including a **pre-existing test this feature does not touch**
(`PermissionNamesAreUnique`) run in isolation. Root cause traced as far as: the permission/identity
seeders are producing zero rows in this sandbox's LocalDB, for reasons not yet diagnosed (each
`dotnet test` process replays all 68 migrations from a freshly-named database, and something in
that path — or in seeding immediately after — leaves the catalogue empty). This was reported to,
and acknowledged by, the human partner on 2026-09-01, who chose to proceed with frontend work
rather than block on root-causing an environment issue outside this feature's scope. See
[`tasks/03-endpoint-and-integration-tests.md`](./tasks/03-endpoint-and-integration-tests.md) for
the full diagnostic trail.

**What this means for the traceability claim:** `AC-806.1`–`AC-806.9` are implemented exactly to
spec and match the design proven correct by the unit tests (handler routing, the `SetAsync`
transaction shape mirrored from the already-working `RevokeAsync`), but are **not yet proven
against real SQL** — the `UPDLOCK` concurrency behaviour, the transaction rollback, and the
composite-key delete are unverified end-to-end. `AC-806.10` (audit) is similarly unverified.

## Contents

| Artifact | What it is |
|---|---|
| [`implementation-plan.md`](./implementation-plan.md) | Backend plan — the atomic `PUT` endpoint, tasks 01–04 |
| [`frontend-implementation-plan.md`](./frontend-implementation-plan.md) | Frontend plan — workbench + global confirmation, tasks 05–12 |
| [`tasks/`](./tasks) | One file per task: files touched, real code, TDD steps, criteria, evidence, deviations |

## Criteria and their tasks

| Criterion | Task | Level | Verified |
|---|---|---|---|
| `AC-806.1` | 03 (unit mapping in 02) | integration | ❌ blocked |
| `AC-806.2` | 02, 03 | unit + integration | unit ✅ / integration ❌ blocked |
| `AC-806.3` | 02, 03 | unit + integration | unit ✅ / integration ❌ blocked |
| `AC-806.4` | 02, 03 | unit + integration | unit ✅ / integration ❌ blocked |
| `AC-806.5` | 02, 03 | unit + integration | unit ✅ / integration ❌ blocked |
| `AC-806.6` | 01, 03 | unit + integration | unit ✅ / integration ❌ blocked |
| `AC-806.7` | 03 | integration | ❌ blocked |
| `AC-806.8` | 03 | integration (concurrent pair) | ❌ blocked |
| `AC-806.9` | 02, 03 | unit + integration | unit ✅ / integration ❌ blocked |
| `AC-806.10` | 04 | integration | ❌ blocked |
| `AC-806.11`–`AC-806.26` | 08–12 (one component) | component | ✅ 31/31 |
| `AC-807.1`, `AC-807.4` | 05, 06 | service unit + component | ✅ 12/12 |
| `AC-807.2`, `AC-807.3` | 06 | component | ✅ |
| `AC-807.5`–`AC-807.7` | 07 | component | ✅ 24/24 |

## Existing criteria this feature touches

| Criterion | What happened to it |
|---|---|
| `AC-805.1` | Unaffected. Its two component tests survive unchanged and pass. |
| `AC-805.2` | Endpoint unchanged; integration coverage at `Integration/PermissionTests.cs:28` is unaffected by code but currently blocked by the same environment issue as this feature's own new integration tests. Its **component** test was rewritten (`AC806_11_TogglingStagesWithoutSendingAnything`) — the screen no longer assigns per click. |
| `AC-805.3` | Same as `AC-805.2`. |
| `AC-805.4` | Rule unchanged (`PermissionAdministrationService.cs:91`). Its component test was rewritten as `AC806_17_BuiltInRoleRefusalKeepsTheStagedChange` — the refusal now arrives from the batch `PUT` during save, not an immediate `DELETE`. |

## Gaps accepted, recorded before implementation

1. **No dynamic authorization policy.** Delivery-plan row 12's other half stays open: policies remain
   role-based. Recorded in the spec's Out of scope and the delivery-plan row for `FEAT-34`.
2. **No role CRUD.** No backend support exists and none is added (spec `A2`).
3. **Cross-role saves are not atomic** (spec `A3`). Mitigated by reporting, not hidden — `AC-806.15`,
   verified by `AC806_15_PartialFailureNamesWhatSavedAndWhatDidNot`.
4. **The two single-mapping endpoints stay unaudited.** Task 04 audits only the batch command; a
   five-line follow-up is recorded at the end of that task file rather than assumed.
5. **`beforeunload` shows the browser's own untranslatable prompt** (spec `A9`). Unavoidable; the
   in-app path uses the real dialog and is fully tested.
6. **Backend integration verification is blocked** by the environment defect described above —
   accepted by the human partner 2026-09-01 as a scope boundary for this implementation pass, not
   silently dropped.

## Deviations from the plan

1. **Tasks 08–12 were implemented and committed as one unit**, not five sequential commits — the
   staged draft, the save flow, failure handling, the guard, and search/grouping/bulk all live in
   the same ~450-line component where later tasks' tests depend on state earlier tasks introduce.
   Recorded in each of those task files.
2. **A validator bug found by TDD** (Task 01): FluentValidation's trailing `.When(...)` disables
   every validator already chained onto that `RuleFor`, not just the one before it. Fixed by
   splitting `NotNull()` onto its own rule.
3. **A `ResponseExtensions.MapFailureStatusCode` gap found by TDD** (Task 01): `Response<T>` does not
   store `MessageType`; HTTP status comes solely from a wire-code switch that did not list the new
   `ERR087`. Fixed in the same file.
4. **A test-only mistake, not a code defect** (Task 09): one test asserted a `ToastService` message
   would appear in the component's own rendered DOM; the toast renders via the app shell's
   `CsToastHost`, not this component. Fixed the assertion.
5. **The guard needed an explicit re-export** (Task 10): `export type { UnsavedChangesHost };` added
   after the `import type`, since a type-only import does not itself re-export.
6. **A TypeScript union-narrowing fix** (Task 11): `columns`'s `flatMap` needed an explicit
   `MatrixColumn[]` return annotation; the plan's inline ternary did not unify on its own.

## Test evidence

**Backend:** 13/13 unit tests pass (`PermissionAdministrationTests`). Integration tests written
(11 new tests across Tasks 03–04) but **blocked** — see the blocker section above and
`tasks/03-endpoint-and-integration-tests.md`.

**Frontend:** 31/31 in `permissions.component.spec.ts` + `permissions-dirty.guard.spec.ts`; 12/12 in
`confirmation.service.spec.ts` + `confirmation-host.component.spec.ts`; 24/24 across
`users`/`departments`/`sla-policies` component specs. Full `common` suite: 223/227 pass (4
pre-existing failures, unrelated file, confirmed via `git status`). Full `admin-app` suite: 257/261
pass (4 pre-existing/FEAT-32-in-progress failures, confirmed unrelated). `npx ng build admin-app`
succeeds; `permissions-component` verified to still code-split as its own lazy chunk.
