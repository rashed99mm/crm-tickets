# FEAT-34 — Role & permission workbench · record

**Epic:** `EPIC-09` · **Stories:** `US-806`, `US-807` ·
**Spec:** [`EPIC-09-US-806-permissions-workbench-and-global-confirmation.md`](../../specs/EPIC-09-US-806-permissions-workbench-and-global-confirmation.md)

**Status: planned, not implemented.** Spec approved 2026-09-01; backend and frontend plans and all
twelve task files written the same day. No implementation commit exists yet, and no test in this
feature has been run. Every task file's **Test evidence** section says so explicitly and stays that
way until output is pasted into it.

## Contents

| Artifact | What it is |
|---|---|
| [`implementation-plan.md`](./implementation-plan.md) | Backend plan — the atomic `PUT` endpoint, tasks 01–04 |
| [`frontend-implementation-plan.md`](./frontend-implementation-plan.md) | Frontend plan — workbench + global confirmation, tasks 05–12 |
| [`tasks/`](./tasks) | One file per task: files touched, real code, TDD steps, criteria, evidence, deviations |

## Criteria and their tasks

Every `AC-n` in the spec maps to exactly one owning task; a criterion with no task is a planning
defect, and a task with no criterion is scope creep.

| Criterion | Task | Level |
|---|---|---|
| `AC-806.1` | 03 (unit mapping in 02) | integration |
| `AC-806.2` | 02, 03 | unit + integration |
| `AC-806.3` | 02, 03 | unit + integration |
| `AC-806.4` | 02, 03 | unit + integration |
| `AC-806.5` | 02, 03 | unit + integration |
| `AC-806.6` | 01, 03 | unit + integration |
| `AC-806.7` | 03 | integration |
| `AC-806.8` | 03 | integration (concurrent pair) |
| `AC-806.9` | 02, 03 | unit + integration |
| `AC-806.10` | 04 | integration |
| `AC-806.11` | 08 | component |
| `AC-806.12` | 08 | component |
| `AC-806.13` | 08 | component |
| `AC-806.14` | 08 | component |
| `AC-806.15` | 09 | component |
| `AC-806.16` | 09 | component |
| `AC-806.17` | 09 | component |
| `AC-806.18` | 10 | component |
| `AC-806.19` | 10 | guard unit + component |
| `AC-806.20` | 10 | component |
| `AC-806.21` | 11 | component |
| `AC-806.22` | 11 | component |
| `AC-806.23` | 11 | component |
| `AC-806.24` | 08 | component |
| `AC-806.25` | 12 | component |
| `AC-806.26` | 12 | component + repo-wide sweep |
| `AC-807.1` | 05, 06 | service unit + component |
| `AC-807.2` | 06 | component |
| `AC-807.3` | 06 | component |
| `AC-807.4` | 05, 06 | service unit + component |
| `AC-807.5` | 07 | component |
| `AC-807.6` | 07 | component |
| `AC-807.7` | 07 | component |

## Existing criteria this feature touches

| Criterion | What happens to it |
|---|---|
| `AC-805.1` | Unaffected. Its two component tests (`permissions.component.spec.ts:56`, `:71`) survive unchanged. |
| `AC-805.2` | Endpoint unchanged and still tested at `Integration/PermissionTests.cs:28`. Its **component** test is rewritten in task 08 — the screen no longer assigns per click. |
| `AC-805.3` | Same as `AC-805.2`; component test rewritten in task 08. |
| `AC-805.4` | Rule unchanged (`PermissionAdministrationService.cs:91`) and still tested at `Integration/PermissionTests.cs:72`. Its component test is rewritten in task 09, where the refusal now arrives from the batch `PUT`. |

## Gaps accepted, recorded before implementation

1. **No dynamic authorization policy.** Delivery-plan row 12's other half stays open: policies remain
   role-based. In the spec's Out of scope, and in the delivery-plan row for `FEAT-34`.
2. **No role CRUD.** No backend support exists and none is added (spec `A2`).
3. **Cross-role saves are not atomic** (spec `A3`). Mitigated by reporting, not hidden — `AC-806.15`.
4. **The two single-mapping endpoints stay unaudited.** Task 04 audits only the batch command;
   extending it is a five-line follow-up, recorded at the end of that task file rather than assumed.
5. **`beforeunload` shows the browser's own untranslatable prompt** (spec `A9`). Unavoidable; the
   in-app path uses the real dialog.

## Deviations from the plan

*None yet — nothing has been executed.*

## Test evidence

*None yet. When tasks run, evidence is pasted into each task file and summarised here.*
