# Auth management — staff accounts, roles and passwords

> **Superseded 2026-08-25 by the platform baseline.** The backend this document describes was
> replaced when the CCE Platform reference was adopted as the CRM baseline — see
> [`EPIC-12-US-000-crm-platform-baseline-design.md`](../specs/EPIC-12-US-000-crm-platform-baseline-design.md).
> The code named below no longer exists in `src/`; it is archived, not deleted. This file is kept
> because it is the record of what was built and why, and deleting it would erase the reasoning
> behind decisions that still hold — the envelope, the localisation approach and the dependency rule
> among them. **Do not follow its steps.**


**Date:** 2026-08-25
**Relates to:** `EPIC-02-US-016-ticket-lifecycle.md` (AC-1..AC-6, AC-67 already delivered)
**Criterion ids:** `AUTH-n`. Separate prefix so this document never collides with `AC-n` or `FND-n`.
**Closes:** the administration half of brief area 10, recorded as gap `G-2`.

## Problem

Sign-in works, but there is no way to create the accounts that sign in. Staff accounts exist only
because a seeder made one. An administrator cannot add an agent, cannot remove access when someone
leaves, cannot see who has access, and nobody can change their own password.

## Assumptions

- **M1.** Two roles only, `Agent` and `Supervisor`, as seeded (ADR-0003). Assigning a role at
  creation is enough; re-roling an existing account is out of scope.
- **M2.** Deactivation is **permanent lockout** (`LockoutEnd = DateTimeOffset.MaxValue`) rather than
  a new `IsActive` column. It needs no migration, and the sign-in path already refuses a locked
  account with `ERR002`, so a deactivated user is refused correctly with no new code on that path.
  **The cost:** deactivation and failed-attempt lockout now share one field, distinguished only by
  whether the date is `MaxValue`. Recorded because it is a real conflation, not a free win.
- **M3.** Supervisors administer accounts. There is no separate Administrator role in S1.
- **M4.** No email is sent. A created account's password is set by the supervisor and passed on out
  of band.

## Out of scope

Password reset by email · self-registration (forbidden by `B3`) · granular permissions beyond the two
roles · the system-wide audit log (the rest of `G-2`) · re-roling an existing account · SSO.

## Acceptance criteria

Priority: **P0** must ship, **P1** should.

### Current user

- **AUTH-1** (P0) Given a valid token, `GET /api/auth/me` returns the caller's id, email, display
  name and roles.
- **AUTH-2** (P0) Given no token, `GET /api/auth/me` returns 401.

### Listing

- **AUTH-3** (P0) Given a `Supervisor`, `GET /api/users` returns every staff account with email,
  display name, roles and active state.
- **AUTH-4** (P0) Given an `Agent`, `GET /api/users` returns 403.

### Creating

- **AUTH-5** (P0) Given a `Supervisor` and valid input, `POST /api/users` creates the account with
  the requested role and returns 201.
- **AUTH-6** (P0) Given an email already in use, then 409 `ERR013` — not 400. The payload is
  well-formed; the state refuses it.
- **AUTH-7** (P0) Given a missing field, a malformed email, or a role outside the two, then 400 with
  errors keyed by field.
- **AUTH-8** (P0) Given a password below the configured strength, then 400 keyed to `password`.
- **AUTH-9** (P0) Given an `Agent`, `POST /api/users` returns 403.
- **AUTH-10** (P0) The created account can immediately sign in.

### Activation

- **AUTH-11** (P0) Given a `Supervisor`, deactivating an account returns 200 and that account can no
  longer sign in — the refusal is `ERR002`, identical to a wrong password, so deactivation is not
  disclosed either.
- **AUTH-12** (P0) Reactivating restores sign-in.
- **AUTH-13** (P0) A supervisor cannot deactivate their own account — 409 `ERR014`. Locking yourself
  out of the only administrative surface is not a valid operation.
- **AUTH-14** (P1) Given an unknown user id, then 404.

### Own password

- **AUTH-15** (P0) Given the correct current password and a valid new one, then 200, and the new
  password works on the next sign-in.
- **AUTH-16** (P0) Given a wrong current password, then 400 keyed to `currentPassword`. Never 401 —
  the caller is authenticated; a field is wrong.
- **AUTH-17** (P0) No response or log line ever contains either password.

### Frontend

- **AUTH-18** (P0) A supervisor sees the staff list with active state, and the page distinguishes
  loading, empty and error.
- **AUTH-19** (P0) A supervisor creates an account through a validated form; server field errors land
  on the control named by their `field`.
- **AUTH-20** (P0) A supervisor deactivates and reactivates from the list, and the row reflects it.
- **AUTH-21** (P0) Any signed-in user changes their own password through a validated form.
- **AUTH-22** (P1) An agent never sees the staff-management nav item, **and** the route refuses them.
  Hiding the link is not the control.

## Design

`IStaffDirectory` in `Application` is the single port; `Infrastructure` implements it over
`UserManager<AppUser>`, which is the only type that touches the store. Endpoints live on `AdminApi`
(staff surface, ADR-0008) behind the existing `Supervisor` policy — which finally gives `AC-4` a real
shipped endpoint to be proven against, closing the partial recorded against `US-114`.

Nine new message keys, each mapped and translated so the `FND-18` guard test stays green:

| Key | Code | Type |
|---|---|---|
| `UserCreated` | `CON003` | Success |
| `UserUpdated` | `CON004` | Success |
| `UserDeactivated` | `CON005` | Success |
| `UserReactivated` | `CON006` | Success |
| `PasswordChanged` | `CON007` | Success |
| `UserEmailExists` | `ERR013` | Conflict |
| `CannotDeactivateSelf` | `ERR014` | Conflict |
| `CurrentPasswordIncorrect` | `VAL011` | Validation |
| `PasswordTooWeak` | `VAL012` | Validation |

Angular: `features/users` and `features/account`, each component as separate `.ts`, `.html` and
`.css` files per the requested convention. The `common` library keeps its existing inline templates —
this is a deliberate local difference for new feature screens, not a migration of what already works.

## Testing

One pass at the end rather than test-first, which is a deliberate trade for a deadline and is
recorded in the plan rather than left to look like an oversight. Integration tests cover
`AUTH-1`..`AUTH-17` against a real database; component tests cover `AUTH-18`..`AUTH-22`.
