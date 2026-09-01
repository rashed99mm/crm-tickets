# FEAT-34 Role & Permission Workbench — Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the permission matrix from a screen that applies every click straight to production
into a workbench: stage a set of changes, review them in a dialog, commit them per role, and see
precisely what landed — plus harden the shared confirmation dialog the workbench depends on and
adopt it on the three destructive admin actions that currently fire unconfirmed.

**Architecture:** Angular 21 standalone components with signals, `OnPush`, zoneless. The screen keeps
its existing `AsyncState` load and gains a **draft layer**: a `Map<roleId, Set<permissionId>>` that
every interaction mutates and nothing sends. A `changes` computed diffs draft against the loaded
snapshot and drives the pending count, the sticky action bar, the confirmation dialog's detail list
and the set of dirty roles. Committing sends one `PUT` per dirty role through the endpoint the
backend plan adds. Shared-library work (`ConfirmationService`, `CsConfirmationHost`,
`PermissionApi`, translations) lands before the screen that consumes it.

**Tech Stack:** Angular 21 (standalone, signals, zoneless), RxJS, Tailwind (design tokens only —
`bg-surface-*`, `text-on-surface-*`, never raw hex), Vitest/Karma via `ng test`,
`HttpTestingController`.

**Spec:** `docs/superpowers/specs/EPIC-09-US-806-permissions-workbench-and-global-confirmation.md`
(`AC-806.11`…`AC-806.26`, `AC-807.1`…`AC-807.7`).

This is the **frontend plan** for the same feature as
[`implementation-plan.md`](./implementation-plan.md). Task 08 consumes the endpoint Task 03 adds, so
the backend tasks land first; tasks 05–07 depend on nothing in the backend and can start immediately.

## Global Constraints

- **Signals, not `ngIf`/`ngFor`.** `@if` / `@for` / `@switch` control flow with `track`, matching
  `permissions.component.html:29-70`. `ChangeDetectionStrategy.OnPush` on every component.
- **No new npm dependency.** Everything here is Angular, RxJS and the existing `common` library.
- **Design tokens only.** Colours come from the token classes already in use
  (`border-border-subtle`, `bg-surface-low`, `text-on-surface-variant`, `bg-error`/`text-on-error`
  for danger). A raw hex or a `red-500` is a defect.
- **Every user-visible string is a translation key** with **both** `en` and `ar` in
  `common/src/lib/i18n/translations.ts`. `TranslationKey` is a union of the literal keys, so a typo
  is a compile error — a dynamic key needs an explicit `Record<string, TranslationKey>` map, never a
  cast.
- **Nothing is applied without a confirmation, and nothing is confirmed without a real dialog.**
  `window.confirm` is not acceptable — it cannot be translated, styled, or tested through the
  fixture.
- **No optimistic UI.** Checked state follows the server (spec `A7`), preserving the rule the
  existing test at `permissions.component.spec.ts:96` was written to protect. The one exception is
  explicit: a *retained* draft after a partial failure is re-overlaid on top of freshly loaded
  server state (`AC-806.15`), never merged into the snapshot.
- **Every test names its criterion** in the test name: `AC806_11_TogglingStagesWithoutRequest`.
- Frontend commands, from `frontend/`: `npx ng test common --watch=false`,
  `npx ng test admin-app --watch=false`, `npx ng build admin-app`.
- Branch `feat/feat-34-permissions-workbench` (shared with the backend tasks); conventional commits.

## The contract between the tasks

Declared once here so no task has to guess a neighbour's names.

```ts
// common/src/lib/ui/confirmation.service.ts — Task 05
export interface ConfirmationRequest {
  readonly id: number;
  readonly title: string;
  readonly message: string;
  readonly details?: readonly string[];   // NEW — rendered as a list (AC-807.4)
  readonly confirmText?: string;
  readonly cancelText?: string;
  readonly danger?: boolean;
}

// common/src/lib/admin/permission.api.ts — Task 08
setRolePermissions(
  roleId: string,
  permissionIds: readonly string[],
  expectedPermissionIds: readonly string[],
): Observable<unknown>;

// admin-app/src/app/features/admin/permissions.component.ts — Task 08
export interface PermissionChange {
  readonly roleId: string;
  readonly roleName: string;
  readonly permissionId: string;
  readonly permissionName: string;
  readonly kind: 'grant' | 'revoke';
}

// The component's public surface the guard in Task 11 depends on:
hasUnsavedChanges(): boolean;
confirmLeave(): Observable<boolean>;
```

## File structure (frontend)

```
frontend/projects/common/src/lib/
  ui/confirmation.service.ts              MODIFY  FIFO queue + details (Task 05)
  ui/confirmation.service.spec.ts         MODIFY  queue tests (Task 05)
  ui/confirmation-host.component.ts       MODIFY  Escape, focus capture/restore (Task 06)
  ui/confirmation-host.component.html     MODIFY  details list, #cancelButton ref (Task 06)
  ui/confirmation-host.component.spec.ts  CREATE  keyboard + focus + details (Task 06)
  admin/permission.api.ts:30-36           MODIFY  setRolePermissions (Task 08)
  i18n/translations.ts:1016-1035          MODIFY  workbench + confirm copy, en + ar (Tasks 07-12)
  testing/rtl-safety.spec.ts              UNCHANGED  already sweeps every new template (Task 12)
  testing/no-hardcoded-strings.spec.ts    UNCHANGED  already sweeps every new template (Task 12)

frontend/projects/admin-app/src/app/
  features/admin/permissions.component.ts        MODIFY  draft layer, save, discard, search, groups, bulk
  features/admin/permissions.component.html      MODIFY  sticky bar, group headers, staged markers
  features/admin/permissions.component.spec.ts   MODIFY  rewrite 2 legacy tests + 16 new
  features/admin/permissions-dirty.guard.ts      CREATE  CanDeactivate (Task 10)
  features/admin/permissions-dirty.guard.spec.ts CREATE  (Task 10)
  features/users/users.component.ts:244          MODIFY  confirm before deactivate (Task 07)
  features/organisation/departments.component.ts:111   MODIFY  confirm (Task 07)
  features/organisation/departments.component.spec.ts  CREATE  no spec file exists today (Task 07)
  features/organisation/sla-policies.component.ts:251   MODIFY  confirm (Task 07)
  app.routes.ts:124-128                          MODIFY  canDeactivate (Task 10)
```

## Tasks

| # | Task | Criteria | Record |
|---|---|---|---|
| 05 | Confirmation queue + `details` | `AC-807.1`, `AC-807.4` (service) | [`tasks/05-confirmation-queue.md`](./tasks/05-confirmation-queue.md) |
| 06 | Dialog keyboard + focus + details rendering | `AC-807.2`, `AC-807.3`, `AC-807.4` | [`tasks/06-confirmation-dialog-keyboard.md`](./tasks/06-confirmation-dialog-keyboard.md) |
| 07 | Adopt the dialog on three destructive actions | `AC-807.5`, `AC-807.6`, `AC-807.7` | [`tasks/07-adopt-confirmation.md`](./tasks/07-adopt-confirmation.md) |
| 08 | Staged editing + save (draft layer, dialog gate, one `PUT` per role) | `AC-806.11`…`AC-806.14`, `AC-806.24` | [`tasks/08-staged-editing-and-save.md`](./tasks/08-staged-editing-and-save.md) |
| 09 | Failure reporting: partial, stale, built-in floor | `AC-806.15`, `AC-806.16`, `AC-806.17` | [`tasks/09-failure-handling.md`](./tasks/09-failure-handling.md) |
| 10 | Discard, Refresh and the unsaved-changes guard | `AC-806.18`, `AC-806.19`, `AC-806.20` | [`tasks/10-discard-refresh-guard.md`](./tasks/10-discard-refresh-guard.md) |
| 11 | Search, resource grouping, per-role bulk buttons | `AC-806.21`, `AC-806.22`, `AC-806.23` | [`tasks/11-search-grouping-bulk.md`](./tasks/11-search-grouping-bulk.md) |
| 12 | Accessibility and RTL pass | `AC-806.25`, `AC-806.26` | [`tasks/12-a11y-and-rtl.md`](./tasks/12-a11y-and-rtl.md) |

**Staging and saving are one task, not two.** A commit where the screen can stage changes but has no
way to commit them leaves the branch holding a screen no reviewer can accept and no user could use;
the two halves are one deliverable.

Task 08 is the one that breaks the legacy component tests at `permissions.component.spec.ts:80` and
`:101`, and Task 09 breaks the one at `:118`. Each rewrites them in its own commit — spec Finding 4.
Do not delete them, and do not leave them failing "to be fixed in a later task": a red test crossing
a commit boundary is indistinguishable from a broken feature. If 08 and 09 are executed separately,
commit them together.

## Task ordering rationale

05 → 06 → 07 first, because the workbench's Save prompt and its navigation prompt can both be
pending at once and the current service would drop one of them (spec Finding 1). Shipping the queue
behind three low-risk adoptions proves it before the complicated screen depends on it.

08 → 09 build the screen in the order a reviewer can check: the happy commit, then every way a
commit can fail. 10 and 11 add the remaining verbs. 12 is a real pass over keyboard, screen-reader
and RTL behaviour, not a polish placeholder — it has its own criteria, and it leans on the two
repo-wide sweeps that already exist (`common/src/lib/testing/rtl-safety.spec.ts`,
`no-hardcoded-strings.spec.ts`) rather than duplicating them.
