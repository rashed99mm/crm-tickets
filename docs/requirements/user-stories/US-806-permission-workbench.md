# US-806 · Edit a role's permissions as one reviewable change

| Field | Value |
|---|---|
| **Story** | `US-806` |
| **Epic** | [EPIC-09 Security & administration](../epics/EPIC-09-administration.md) |
| **Feature** | [`FEAT-34` Role & permission workbench](../delivery-plan.md#assessment-sprint-3--role--permission-workbench) |
| **Layer** | Backend + Frontend |
| **Ships with** | Both layers of this story, and [US-807](./US-807-global-confirmation.md) — the workbench's Save, Discard and navigation prompts are the dialog US-807 hardens. Neither ships alone. |
| **Rule proposal** | Realises the reserved `US-075 Manage Roles` / `US-076 Manage Permissions` backlog rows on `EPIC-09` — permission-set editing only, not role CRUD |
| **Actor** | Administrator |
| **Priority** | P1 |
| **Sprint** | 21 — Role & permission workbench · Slice S9 |
| **Estimate** | 13 points |
| **Status** | `not started` |
| **BRD requirements** | §8 administrative configuration |
| **Spec criteria** | AC-806.1 … AC-806.26 |
| **Depends on** | US-804 (permission entity), US-805 (permission admin UI) — both implemented |

## Story

**As an administrator**, **I want** to stage a set of permission changes and review them before they
take effect, **so that** re-scoping a role is one deliberate, confirmable change rather than eight
irreversible clicks that each go live the moment I make them.

## Business rules

- A role's permission set is replaced atomically. Every change staged for one role lands, or none of
  them do (spec `A3`).
- A built-in role must always keep at least one permission. Existing rule, preserved through the new
  path — `PermissionAdministrationService.cs:11-21` and `:91`.
- A save computed from a stale view of a role is refused, not merged (spec `A6`). The administrator
  sees what changed, reloads, and decides again.
- Nothing reaches the server until the administrator confirms it. Staging is local and free.
- Roles themselves are not created, renamed or deleted here (spec `A2`).

## Acceptance criteria

Criteria are cited from
[the spec](../../superpowers/specs/EPIC-09-US-806-permissions-workbench-and-global-confirmation.md),
not paraphrased. The spec is authoritative; if this file and the spec disagree, the spec is right and
this file is stale.

**Backend — `AC-806.1`…`AC-806.10`:** the atomic `PUT /api/admin/permissions/{roleId}`; the
built-in-role floor as a 409 that changes nothing; unknown role and unknown permission as 404s;
the stale-snapshot 409; field-keyed 400s for duplicate or empty ids; 403 for a caller outside the
`UserManagement` policy; a deterministic outcome for two concurrent saves; a no-op set that writes
nothing; and an audit entry for a successful change.

**Frontend — `AC-806.11`…`AC-806.26`:** toggling stages without any request; Save opens a dialog
listing every staged grant and revoke; accept sends one request per dirty role; cancel keeps the
draft; partial failure retains only the failed role and reports `n of m`; stale and built-in
refusals get their own messages; Discard, navigation and Refresh all confirm when dirty; search and
resource grouping with per-group counts; per-role Grant all / Revoke all that stage rather than
apply; disabled actions and no sticky bar when clean; staged state not conveyed by colour alone with
an `aria-live` pending count; and correct RTL rendering.

## SQL tables

None new. The endpoint writes existing `RolePermissions` rows
(`Persistence/Configurations/RolePermissionConfiguration.cs`). No migration.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-806.1 | Integration | `planned` | role holds {A,B} / PUT {B,C} | 200 `CON079`, mappings exactly {B,C} |
| TC-02 | AC-806.2 | Integration | `planned` | built-in role / PUT `[]` | 409 `ERR002`, every mapping retained |
| TC-03 | AC-806.5 | Integration | `planned` | expected set no longer current / PUT | 409 `ERR087`, nothing applied |
| TC-04 | AC-806.8 | Integration | `planned` | two concurrent PUTs, same snapshot | exactly one 200, one 409 `ERR087` |
| TC-05 | AC-806.3, AC-806.4, AC-806.6 | Unit | `planned` | unknown ids / duplicates / null expectations | 404s and field-keyed 400s |
| TC-06 | AC-806.10 | Integration | `planned` | successful set / read audit log | one `Updated`/`Role` row for the acting user |
| TC-07 | AC-806.11, AC-806.12, AC-806.14 | Component | `planned` | toggle, Save, cancel | no request on toggle; dialog lists changes; cancel sends nothing |
| TC-08 | AC-806.13, AC-806.15 | Component | `planned` | two dirty roles, one PUT fails | one request per role; failed role's draft retained; `n of m` banner |
| TC-09 | AC-806.16, AC-806.17 | Component | `planned` | 409 `ERR087` / 409 `ERR002` | distinct messages; Reload offered for stale |
| TC-10 | AC-806.18…AC-806.20 | Component + guard | `planned` | Discard / navigate away / Refresh while dirty | each confirms; declining retains the draft |
| TC-11 | AC-806.21…AC-806.23 | Component | `planned` | search, collapse, Grant all | columns filter; collapse keeps staged changes; bulk stages only real changes |
| TC-12 | AC-806.24…AC-806.26 | Component | `planned` | clean draft / staged cell / Arabic locale | actions disabled, no sticky bar; non-colour marker + `aria-live`; RTL intact |

## Notes

The screen's current behaviour is not merely unpolished, it is unsafe: `permissions.component.ts:77`
applies a revoke on the click, with no confirmation and no undo. That is the defect this story
exists to remove; the search, grouping and bulk buttons are what make the staged model usable at
catalogue scale.

Two existing component tests (`permissions.component.spec.ts:80` and `:101`) assert the
click-applies-immediately contract and are rewritten here. `AC-805.2`/`AC-805.3` keep their
integration coverage — see the spec's Finding 4.

## Open questions

None. Optimistic concurrency, per-role atomicity and the four UX additions were all decided with the
human partner on 2026-09-01 and are recorded in the spec.

## Status evidence

`not started` — spec approved 2026-09-01, backend and frontend plans written the same day, no code
written. Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
