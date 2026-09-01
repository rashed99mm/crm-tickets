# Role & permission workbench + global confirmation

**Epic:** `EPIC-09` · **Feature:** `FEAT-34` · **Stories:** `US-806`, `US-807` ·
**Status:** spec approved 2026-09-01 · **not implemented**

> `FEAT-33` was already taken by
> [ticket triage and BI alignment](./EPIC-02-US-926-ticket-triage-and-bi-alignment.md), so this
> feature is `FEAT-34`. Checked against every `FEAT-3n` reference in `docs/` before numbering.

## Problem

The permission screen works and is unsafe to use.

`frontend/projects/admin-app/src/app/features/admin/permissions.component.ts:77-106` applies every
checkbox click straight to the server: one click is one `POST` or `DELETE`, followed by a full
reload of the matrix. Four consequences, all of them things an administrator hits on the first
real session:

1. **No confirmation, and no undo.** Un-ticking a box strips a permission from every member of that
   role the instant the pointer lands. There is no "are you sure", and the only way back is to
   remember what was there and tick it again. Revoking `ticket.close` from `Agent` is one
   mis-click.
2. **No way to make a set of related changes.** Re-scoping a role means eight clicks, eight
   requests, eight reloads, and eight intermediate states that are each briefly live in
   production. If the admin abandons halfway, the role is left in a state nobody designed.
3. **The matrix does not scale.** Ten permissions × eight roles already needs a horizontal scroll
   (`permissions.component.html:35`); the catalogue in `PermissionSeeder.cs:11-22` is grouped by
   resource (`ticket.*`, `customer.*`, `report.*`, `user.*`) but the screen shows one flat run of
   columns with no search, no grouping, and no per-role totals.
4. **One button on the whole page.** `Refresh` (`permissions.component.html:11-14`) is the only
   action. There is no Save, no Discard, no bulk action — the screen has no verbs.

Two further defects sit behind it, found by reading the shared code this feature must use:

5. **`ConfirmationService.confirm()` leaks a caller.** `confirmation.service.ts:24-28` overwrites
   `pending` without resolving the request it displaces, so the displaced caller's `Observable`
   never emits and never completes. With one caller in the codebase today
   (`kb-admin.component.ts:335-352`) this is latent; this feature adds several and would trip it.
6. **The confirmation dialog cannot be dismissed from the keyboard.**
   `confirmation-host.component.html` renders `role="alertdialog"` with `aria-modal="true"` but has
   no Escape handler, sets no initial focus, and does not restore focus to the trigger on close.
7. **Permission changes are not audited.** `AuditBehavior.cs:17-23` lists eleven auditable
   commands; `AssignPermissionCommand` and `RevokePermissionCommand` are not among them. Changing
   who can do what in the system leaves no trail, while creating a notification leaves one.

Three destructive admin actions elsewhere fire with no confirmation at all:
`users.component.ts:244` (deactivate a user), `departments.component.ts:111`,
`sla-policies.component.ts:251`.

## Decisions already made (with the human partner, 2026-09-01)

- **Staged editing, not instant apply.** Ticking a box stages a change locally; a sticky action bar
  carries `Save n changes` and `Discard`; Save opens the global confirmation dialog listing every
  staged grant and revoke before anything is sent.
- **A new atomic backend endpoint**, rather than composing the existing single-mapping endpoints N
  times from the browser. A role's permission set is replaced in one transaction: all of that
  role's changes land, or none do.
- **Optimistic concurrency is in scope.** A full-set `PUT` is silently last-write-wins otherwise —
  admin B's save would erase admin A's grants from thirty seconds earlier with no error anywhere.
  The client sends the snapshot it staged from and a mismatch is refused.
- **All four UX additions are in scope:** search + group-by-resource, per-role bulk buttons, an
  unsaved-changes guard, and adopting the shared dialog on the three unconfirmed destructive
  actions named above.

## Assumptions

Numbered; each written so it can be proven wrong.

- **A1. Grouping derives from the permission name, not from new schema.** The group is the substring
  before the first `.` in `Permission.Name` (`ticket`, `customer`, `report`, `user` per
  `PermissionSeeder.cs:11-22`). A name with no `.` groups under `other`. No group table, no
  migration, no change to the seeded catalogue.
- **A2. Roles are not created, renamed or deleted here.** No backend role CRUD exists and none is
  added. "Role editor" in delivery-plan row 12 is read as *editing a role's permission set*, which
  is what this feature delivers. Creating roles stays unbuilt and unclaimed.
- **A3. The batch is per role.** A save touching three roles is three requests — atomic per role,
  **not** atomic across roles. A partial outcome is possible and is reported precisely rather than
  hidden (`AC-806.14`).
- **A4. `expectedPermissionIds` is required, and may be empty.** Set-equality, order-insensitive. A
  role with no permissions sends `[]`. There is no "skip the check" mode: an omitted or null value
  is a 400, because a nullable staleness guard is a guard nobody can rely on.
- **A5. The built-in-role floor is unchanged.** The role names in
  `PermissionAdministrationService.cs:11-21` must keep at least one mapping; the batch endpoint
  cannot empty them. A non-built-in role may legitimately be emptied.
- **A6. A stale save is refused, never merged.** No three-way merge, no field-level reconciliation.
  The admin reloads and re-stages. Auto-merging permission grants is how a revoke silently
  un-revokes itself.
- **A7. No optimistic UI.** After a successful save the component reloads from the server, keeping
  the existing rule at `permissions.component.ts:92` that checked state follows the server, never
  the click.
- **A8. The two single-mapping endpoints stay.** `AC-805.2`/`AC-805.3` are tested against them
  (`Integration/PermissionTests.cs:28`) and they are not deprecated, removed or reimplemented in
  terms of the batch.
- **A9. The unsaved-changes guard has two halves with different fidelity.** In-app navigation uses
  `CanDeactivate` and the styled global dialog; a browser tab close uses `beforeunload`, where the
  browser shows its own untranslatable text. The second is a best-effort backstop, not a designed
  screen.
- **A10. The confirmation queue is FIFO.** Realistic depth is 2 (a guard prompt arriving while a
  save prompt is open). No cap, no coalescing, no priority.
- **A11. No new E2E journey.** S1's single-Playwright-journey rule (`AC-64`) stands; every criterion
  here is served by unit, integration and component tests.
- **A12. The adoption list is exactly three call sites.** `users.component.ts:244` (deactivate
  only — activating is not destructive), `departments.component.ts:111`,
  `sla-policies.component.ts:251`. `customer-detail.component.ts:225` already has a bespoke inline
  confirm and `kb-admin.component.ts:335` already uses the service; neither is migrated here.

## Out of scope

- **Role CRUD** — creating, renaming or deleting roles (A2).
- **Permission catalogue CRUD** — permissions are seeded (`PermissionSeeder.cs`); no admin UI to
  invent new ones.
- **Dynamic authorization policy built from assigned permissions.** This is the *other* half of
  delivery-plan row 12's recorded gap and stays open: today's policies are still role-based
  (`[Authorize(Policy = "UserManagement")]`, `PermissionsController.cs:20`). Editing the matrix
  changes `RolePermissions` rows and what `PermissionAuthorizationHandler` reads; it does not
  rewrite the role-based policies. **Not a regression introduced here, and not fixed here.**
- **Per-user permission overrides**, permission groups/bundles, role cloning.
- Migrating `customer-detail.component.ts`'s inline confirm to the shared dialog (A12).
- `portal-app` — no permission surface exists there.
- Undo-after-save. Discard reverts *staged* changes only; a committed save is reverted by staging
  the inverse.

## Acceptance criteria

`AC-805.1`–`AC-805.4` remain in force and remain tested. `AC-806.x` and `AC-807.x` are additive.

### US-806 · Backend — atomic role permission set

- **AC-806.1** Given an administrator and a role holding `{A, B}`, when `PUT
  /api/admin/permissions/{roleId}` is sent with `permissionIds = {B, C}` and a matching
  `expectedPermissionIds`, then the response is 200 with code `CON079`, and the role's mappings are
  exactly `{B, C}` — the add and the remove both applied in one transaction.
- **AC-806.2** Given a **built-in** role, when the request sets `permissionIds = []`, then the
  response is 409 with code `ERR002` and **no mapping is removed** — the role still holds every
  permission it held before the call.
- **AC-806.3** Given a request whose `permissionIds` contains an id no `Permission` row matches,
  then the response is 404 with code `ERR001` and no mapping is added or removed.
- **AC-806.4** Given a `roleId` no role matches, then the response is 404 with code `ERR001`.
- **AC-806.5** Given `expectedPermissionIds` that does not set-equal the role's currently stored
  set, then the response is 409 with code `ERR087` and nothing is applied.
- **AC-806.6** Given `permissionIds` containing a duplicate id or `Guid.Empty`, or a null
  `expectedPermissionIds`, then the response is 400 carrying field-keyed `errors[]` naming
  `permissionIds` / `expectedPermissionIds`.
- **AC-806.7** Given a caller whose role does not satisfy the `UserManagement` policy, when any
  permission endpoint is called, then the response is 403.
- **AC-806.8** Given two concurrent `PUT`s for the same role staged from the same snapshot, then
  exactly one returns 200 and the other returns 409 `ERR087`; the role is never left holding an
  unintended set, and a built-in role is never left empty.
- **AC-806.9** Given `permissionIds` set-equal to what the role already holds, then the response is
  200 and no mapping row is inserted or deleted.
- **AC-806.10** Given a successful set-permissions call, then an `AuditLog` row is written naming
  the acting user, action `Updated`, entity type `Role`, and the role's id. *(Added during
  grounding — see Finding 3.)*

### US-806 · Frontend — the workbench

- **AC-806.11** Given the matrix loaded, when a checkbox is toggled, then **no HTTP request is
  issued**, the cell shows a staged marker, and the pending-change count increments.
- **AC-806.12** Given staged changes, when `Save` is pressed, then the global confirmation dialog
  opens listing every staged grant and revoke by role and permission name, and no request is issued
  until it is accepted.
- **AC-806.13** Given the dialog is accepted, then one `PUT` per dirty role is sent carrying that
  role's `permissionIds` and the `expectedPermissionIds` it was staged from; on success the draft
  clears, a success toast shows, and the matrix reloads from the server.
- **AC-806.14** Given the dialog is cancelled, then no request is issued and every staged change is
  retained.
- **AC-806.15** Given two dirty roles where one `PUT` fails, then the succeeded role's staged
  changes clear, the failed role's are **retained**, and a result banner states how many of how many
  roles saved and names the failed role with the server's reason.
- **AC-806.16** Given a `PUT` refused with `ERR087`, then the message states the role was changed by
  someone else and offers a `Reload` action which reloads the matrix and drops that role's draft.
- **AC-806.17** Given a `PUT` refused with `ERR002`, then the built-in-role message
  (`permissions.lastRequired`) is shown and that role's staged changes are retained.
- **AC-806.18** Given staged changes, when `Discard` is pressed, then a confirmation dialog opens;
  accepted resets the draft to the loaded snapshot, cancelled retains it.
- **AC-806.19** Given staged changes, when the admin navigates to another route, then a
  confirmation dialog opens; declining keeps them on the page with the draft intact, accepting
  leaves and discards it.
- **AC-806.20** Given staged changes, when `Refresh` is pressed, then it confirms first; with a
  clean draft it reloads immediately with no dialog.
- **AC-806.21** Given a search term, then only permissions whose name or description matches render
  as columns; a term matching nothing shows an in-table "no permission matches" message and
  **not** the page-level empty state; `Clear` restores every column.
- **AC-806.22** Given the matrix loaded, then permissions render grouped by resource prefix with an
  `assigned / total` count per role per group, and a group can be collapsed; collapsing a group
  hides its columns without discarding staged changes inside it.
- **AC-806.23** Given a role row, when `Grant all` or `Revoke all` is pressed, then every currently
  visible (search- and collapse-filtered) permission for that role is **staged**, nothing is sent,
  and the pending count reflects only cells that actually changed.
- **AC-806.24** Given no staged changes, then `Save` and `Discard` are disabled and the sticky
  action bar is not rendered.
- **AC-806.25** Given a staged cell, then its staged state is conveyed by text or icon and not by
  colour alone, and the pending-change count is announced through an `aria-live` region.
- **AC-806.26** Given the Arabic locale, then the sticky action bar, the group headers and the
  confirmation dialog render correctly under `dir="rtl"` with no clipped or mirrored-away controls.

### US-807 · Global confirmation

- **AC-807.1** Given a confirmation is pending, when a second `confirm()` is requested, then both
  callers receive a result: the second dialog opens after the first resolves and **no caller
  observable is left unresolved or uncompleted**.
- **AC-807.2** Given a confirmation dialog is open, when `Escape` is pressed, then it closes and
  resolves `false`.
- **AC-807.3** Given a confirmation dialog opens, then focus moves into it (the cancel control for
  a `danger` request); when it closes, focus returns to the element that opened it.
- **AC-807.4** Given a request carrying `details`, then each detail renders as its own list item
  under the message.
- **AC-807.5** Given the users screen, when `Deactivate` is pressed on an active user, then a
  `danger` confirmation opens; cancelling issues no request, accepting issues the existing
  activate/deactivate call. Pressing `Activate` on an inactive user does **not** confirm.
- **AC-807.6** Given the departments screen, when `Deactivate` is pressed, then a `danger`
  confirmation opens and cancelling issues no request.
- **AC-807.7** Given the SLA policies screen, when `Deactivate` is pressed, then a `danger`
  confirmation opens and cancelling issues no request.

## Design

### Backend

One new endpoint on the existing controller (`PermissionsController.cs:21`), which keeps its
`[Authorize(Policy = "UserManagement")]` class attribute — that is what satisfies `AC-806.7`
without new code:

```
PUT /api/admin/permissions/{roleId:guid}
{
  "permissionIds":         ["…", "…"],
  "expectedPermissionIds": ["…", "…"]
}
```

| Layer | File | Change |
|---|---|---|
| Application | `Features/Admin/Commands/SetRolePermissions/SetRolePermissionsCommand.cs` | new record command + request DTO |
| Application | `…/SetRolePermissionsCommandValidator.cs` | mirrors `AssignPermissionCommandValidator.cs:7-14`, plus distinctness and non-null expectations |
| Application | `…/SetRolePermissionsCommandHandler.cs` | result switch mirroring `RevokePermissionCommandHandler.cs:18-26` |
| Application | `Interfaces/IPermissionAdministrationService.cs` | `SetAsync`; `PermissionMutationResult` gains `StaleSnapshot` |
| Application | `Errors/ApplicationErrors.cs:133-141` | `UPDATED`, `STALE_SNAPSHOT` |
| Application | `Messages/SystemCode.cs`, `Messages/SystemCodeMap.cs` | `CON079`, `ERR087` (first free after `CON078`/`ERR086`) |
| Application | `Behaviors/AuditBehavior.cs:17-38` | register the new command; entity type `Role` |
| Api.Shared | `Localization/Resources.yaml:41-58` | `PERMISSION_UPDATED`, `PERMISSION_STALE_SNAPSHOT` en+ar |
| Infrastructure | `Security/PermissionAdministrationService.cs` | `SetAsync` |
| InternalApi | `Controllers/PermissionsController.cs` | `PUT {roleId:guid}` action |

**No migration.** The endpoint writes existing `RolePermissions` rows.

`SetAsync` reuses the locking shape already proven at
`PermissionAdministrationService.cs:83-101` — `CreateExecutionStrategy` wrapping a transaction whose
first read takes `WITH (UPDLOCK)` on the role's mapping rows. That read serves three purposes at
once: it is the staleness comparison (`AC-806.5`), the built-in floor check (`AC-806.2`), and the
diff source. Because the lock is taken before any decision, `AC-806.8`'s concurrent pair resolves
deterministically — the second transaction blocks, re-reads, finds its expected set no longer
current, and is refused.

Order of refusals inside the lock, most specific first: role missing → unknown permission id →
stale snapshot → built-in floor. Each returns before any write.

### Frontend — draft layer over the existing load

`permissions.component.ts` keeps its `AsyncState<PermissionAdministration>` load
(`permissions.component.ts:59-69`) and gains a draft on top:

- `draft = signal<ReadonlyMap<roleId, ReadonlySet<permissionId>>>` — seeded from the snapshot on
  every load, mutated by every toggle and bulk action, and **never** sent to the server on its own.
- `changes = computed<PermissionChange[]>` — the diff of draft against snapshot, each entry
  `{ roleId, roleName, permissionId, permissionName, kind: 'grant' | 'revoke' }`. It drives the
  pending count, the sticky bar, the dialog's `details`, and `dirtyRoleIds`.
- `groups = computed<PermissionGroup[]>` — permissions bucketed by A1's prefix rule, filtered by the
  search term, each carrying its collapsed flag and per-role assigned counts.

`toggle()` replaces today's HTTP call (`permissions.component.ts:77-106`) with a draft mutation.
`save()` confirms, then sends one `PUT` per dirty role via
`PermissionApi.setRolePermissions(roleId, permissionIds, expectedPermissionIds)`, collecting a
per-role outcome so `AC-806.15` can report `2 of 3 roles saved` with the failure named.

The guard is a `CanDeactivateFn` in `admin-app` (there is no `CanDeactivate` precedent in the
codebase; `common/src/lib/auth/guards.ts` holds `CanActivateFn`s only) registered on the
`permissions` route at `app.routes.ts:125-128`. It asks the component whether it is dirty and routes
the question through `ConfirmationService`, so `AC-806.19` shares the dialog with `AC-806.12` — and
is precisely why `AC-807.1`'s queue must exist first.

### Shared dialog

`ConfirmationRequest` gains `details?: readonly string[]`. `ConfirmationService` replaces its single
`pending` signal with a FIFO queue so a displaced request is no longer dropped mid-flight
(`AC-807.1`). `CsConfirmationHost` gains an Escape handler, initial focus, and focus restoration
(`AC-807.2`, `AC-807.3`), and renders `details` as a list (`AC-807.4`).

All three additions are backwards compatible: `kb-admin.component.ts:335-352` passes no `details`
and keeps working unchanged.

### Findings from grounding

Recorded here, not just in the plan, per the SDD skill.

1. **`ConfirmationService` drops a displaced caller** (`confirmation.service.ts:24-28`) — a live
   bug, latent only because there is one caller today. Fixed by `AC-807.1`.
2. **The dialog is a keyboard trap** — `role="alertdialog"`, `aria-modal="true"`, no Escape, no
   focus management (`confirmation-host.component.html`). Fixed by `AC-807.2`/`AC-807.3`.
3. **Permission changes are unaudited** (`AuditBehavior.cs:17-23`). `AC-806.10` closes this for the
   new endpoint. Two wrinkles: `ResolveEntityId` (`AuditBehavior.cs:120-133`) reads a request
   property named exactly `Id`, and the new command's is `RoleId`, so the resolver needs a `RoleId`
   fallback or the entry silently skips (line 91-96); and this does **not** retroactively audit the
   two single-mapping endpoints — extending `AuditableCommands` to those is a one-line addition
   each, offered as an optional task rather than assumed.
4. **Two existing tests assert the contract this feature deliberately changes.**
   `permissions.component.spec.ts:80` (`AC805_2_AssignPermissionToRole`) and `:101`
   (`AC805_3_RevokePermissionFromRole`) both assert that a checkbox click fires `POST`/`DELETE`
   immediately. Staged editing makes that false at the component level. `AC-805.2`/`AC-805.3`
   remain satisfied — the endpoints still assign and revoke, and
   `Integration/PermissionTests.cs:28` still proves it — so those two component tests are rewritten
   to assert *stage then save*, and the criteria keep integration-level coverage. This is a
   deliberate contract change, recorded before implementation rather than discovered in review.

## Test strategy

| Level | Where | Covers |
|---|---|---|
| Unit (backend) | `tests/Unit/Features/Admin/PermissionAdministrationTests.cs` | handler result mapping and validator refusals — `AC-806.3`…`AC-806.6` |
| Integration | `tests/Integration/PermissionTests.cs` | real endpoint against real SQL — `AC-806.1`, `AC-806.2`, `AC-806.5`, `AC-806.7`…`AC-806.10`, and the concurrent pair, mirroring the existing `ConcurrentRevokesLeaveBuiltInRoleWithOneMapping` at `:105` |
| Component | `features/admin/permissions.component.spec.ts` | staging, dialog gating, per-role save, partial failure, search, grouping, bulk, a11y — `AC-806.11`…`AC-806.26` |
| Unit (frontend) | `common/src/lib/ui/confirmation.service.spec.ts` | queue behaviour — `AC-807.1` |
| Component | new `confirmation-host.component.spec.ts` | Escape, focus, `details` — `AC-807.2`…`AC-807.4` |
| Component | `users`, `departments`, `sla-policies` specs | confirm-before-deactivate — `AC-807.5`…`AC-807.7` |
| Guard | new `permissions-dirty.guard.spec.ts` | `AC-806.19` |

Every test names its criterion — `[Trait("AC", "806.1")]` on the backend, `AC806_11_…` in the
frontend test name — so "show me where `AC-806.14` is tested" is answerable by grep.

## Traceability

| Story | Layer | Criteria | Ships with |
|---|---|---|---|
| `US-806` | Backend | `AC-806.1`…`AC-806.10` | `US-806` frontend |
| `US-806` | Frontend | `AC-806.11`…`AC-806.26` | `US-806` backend |
| `US-807` | Frontend | `AC-807.1`…`AC-807.7` | — cross-cutting; server half already exists for all three adopted actions |
