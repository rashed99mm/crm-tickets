# EPIC-13 frontend implementation plan

**Spec:** [`../../specs/EPIC-11-US-701-mockup-faithful-ui.md`](../../specs/EPIC-11-US-701-mockup-faithful-ui.md)  
**Epic:** `EPIC-13`  
**Layer:** Frontend only, cross-cutting over existing vertical features  
**Status:** Planned

## Working rules

- Use Angular standalone components, signals, `OnPush`, shared `common` components and logical
  Tailwind utilities.
- Do not add a backend endpoint to make a visual region look populated.
- For every task: write a failing test naming its `AC-*`, implement the smallest change, run focused
  tests, then run the relevant app tests and build.
- Keep one feature's backend/frontend gate intact when a task touches an existing feature.
- Record actual test output and deviations in the task record before marking the task complete.

## Task sequence

| Task | Scope | Criteria | Commit boundary |
|---|---|---|---|
| 01 | Audit references and freeze token map | `AC-400`…`AC-404` | token/shell contract |
| 02 | Build responsive shared shell primitives | `AC-400`, `AC-405`, `AC-413`…`AC-415`, `AC-418` | shell primitives |
| 03 | Adapt shell, landing and signup | `AC-405`, `AC-412`, `AC-418` | shell/auth screens |
| 04 | Adapt ticket surfaces | `AC-407`…`AC-409`, `AC-416`, `AC-417`, `AC-418` | ticket screens |
| 05 | Adapt customer and admin surfaces | `AC-410`, `AC-411`, `AC-416`, `AC-417`, `AC-418` | customer/admin screens |
| 06 | Adapt dashboards, analytics and portal | `AC-406`, `AC-411`, `AC-412`, `AC-416`, `AC-417`, `AC-418` | dashboard/portal screens |
| 07 | Accessibility, i18n and responsive hardening | `AC-404`, `AC-413`…`AC-418` | responsive/accessibility |
| 08 | Visual matrix and regression closure | `AC-419`…`AC-422` | verification |

## Execution order

Task 01 must finish before markup changes because it resolves palette and token mapping. Task 02
must finish before screen adaptation because all screens use its shell and state contracts. Tasks
03–06 are screen verticals and may be delivered one at a time, but each must finish its focused
tests and build before the next screen group starts. Tasks 07 and 08 are terminal hardening tasks.

## Verification commands

Run from `frontend/`:

```text
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test
```

Only commands that exist in the current workspace may be used; if a script or route is absent, the
task record must state that gap and the plan must be amended rather than claiming verification.

## File-level execution map

| Area | Existing entry points | Planned files to change or add |
|---|---|---|
| Tokens | `frontend/projects/common/src/styles/theme.css` | Same file: add scoped Proton tokens and document mappings |
| Shared UI | `frontend/projects/common/src/lib/ui/*.component.{ts,html}` | Extend existing primitives; add repeated patterns such as `responsive-drawer.component.{ts,html}` only when reuse is proven |
| Staff shell | `frontend/projects/admin-app/src/app/layout/shell.component.{ts,html}` | Same files: responsive drawer state and palette attribute |
| Staff routes | `frontend/projects/admin-app/src/app/app.routes.ts` | Same file only when a supplied screen needs an existing route alias; no unowned route |
| Ticket queue | `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.{ts,html}` | Same files and `ticket-queue.component.spec.ts` |
| Ticket create | `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.{ts,html}` | Same files and `ticket-create.component.spec.ts` |
| Ticket detail | `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.{ts,html}` | Same files, `ticket-messages.component.{ts,html}`, `ai-panel.component.{ts,html}` and specs |
| Customer workspace | `frontend/projects/admin-app/src/app/features/customers/customer-detail.component.{ts,html}` | Same files, `customer-notes.component.{ts,html}`, `customer-attachments.component.{ts,html}` and specs |
| Customer/admin tables | `frontend/projects/admin-app/src/app/features/customers/customer-list.component.{ts,html}`, `features/users/users.component.{ts,html}` | Same files and existing specs |
| Dashboard/reports | `features/dashboard/dashboard.component.{ts,html}`, `features/reports/*-report.component.{ts,html}` | Same files and existing specs |
| Admin settings | `features/admin/{audit-log,permissions,platform-settings}.component.{ts,html}` | Same files and existing specs where present |
| Portal shell/features | `frontend/projects/portal-app/src/app/layout/shell.component.{ts,html}`, `features/{home,dashboard,auth,tickets,kb}/*` | Same files; add missing profile only if the existing route contract requires it |
| Cross-cutting tests | `frontend/projects/common/src/lib/testing/{rtl-safety,no-hardcoded-strings}.spec.ts` | Extend assertions for newly adapted templates |
| E2E | `frontend/e2e/` and `frontend/playwright.config.ts` | Add one visual matrix spec and stable route fixtures; do not duplicate the terminal journey |

## Concrete execution example

For `ticket_queue`, read:

```text
stitch_smart_support_ticketing_crm/ticket_queue/code.html
frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts
frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html
frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.spec.ts
frontend/projects/common/src/lib/ui/status-pill.component.ts
frontend/projects/common/src/styles/theme.css
```

Write the failing test `AC407_QueueUsesReferenceTableCompositionAndStates`, port the reference table
structure into `ticket-queue.component.html`, map status colours to `CsStatusPill`, and run the
focused spec before the full app suite. The task is incomplete if only the successful API branch is
tested; loading, empty and error branches must also be exercised.

## Per-task execution record

Each task file below is an executable checklist. Before implementation, add the planned test names
to the relevant spec file and observe the failure. After implementation, update the task status,
commit hash, command output and deviations. Never mark a task done from a code read or an unrun build.
