# Task 0 — The role vocabulary

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — task 0 |
| Feature | `FEAT-07` (blocks every task after it) |
| Criteria | assumption `A2`; enables `AC-42`…`AC-47` |
| Status | `done` |
| Commit | uncommitted — working tree |
| Decision | [ADR-0012](../../../../adr/0012-seed-agent-and-supervisor-alongside-the-inherited-roles.md) |

## Files

- `src/CustomerSupport.Domain/Entities/Identity/ApplicationRole.cs` (`Roles.Agent`, `Roles.Supervisor`)
- `src/CustomerSupport.Infrastructure/Seeders/IdentitySeeder.cs`
- `src/CustomerSupport.Api.Shared/Extensions/AuthorizationExtensions.cs`
- `tests/CustomerSupport.Tests/Integration/CrmApiFactory.cs`

## Test evidence

Every `AC-42`…`AC-47` test signs in a user holding one of these roles, so the whole feature is the
evidence. Suite: **233 passed, 0 failed.**

## Why this was not a question for the user

Day 1's records flagged the role vocabulary as blocking `FEAT-07`, and it was tempting to stop and
ask. On reading, the **approved spec had already decided it**: assumption `A2` says "two roles are
sufficient: `Agent` and `Supervisor`", `erd.md` §6 agrees, and four criteria name the roles in their
own text.

The platform's six roles are an inherited artefact, not a requirement. Asking would have stalled the
phase over a decision the specification made before the platform was adopted.

## Deviations from the plan

**1. `Supervisor` is granted wherever `Admin` is; `Agent` is not.**
Planned and delivered. An administrator should not be locked out of supervisory actions, but
`Admin` is deliberately **not** treated as an agent — "can administer the platform" and "works a
support queue" are different claims, and `AC-44` turns on the second. Without that asymmetry the
seeded administrator would be a valid assignment target for every ticket.

**2. The factory's role creation had to be made race-tolerant, and it broke unrelated tests first.**
`IdentitySeeder` now seeds these two on every host start. `CrmApiFactory.CreateUserAsync` also
creates roles on demand. Test classes run in parallel and each starts a host, so check-then-create
collided on `RoleNameIndex` — and the failures appeared in `ChangePasswordEndpointTests` and
`TicketEndpointTests`, files with nothing to do with this feature.

Third time this phase pattern has bitten (categories, migrations, now roles). It is a property of
running many hosts against one shared database, and every seeder needs to expect it.

## The cost, stated

The system now carries **eight roles across two vocabularies**. A reader has to know that
`User`/`Admin`/`ContentManager` belong to the inherited platform surface while `Agent`/`Supervisor`
belong to the support domain. That is real conceptual debt, ADR-0012 records it as such, and
collapsing the two belongs to whatever work re-specifies the knowledge-base and user-administration
features — not to the phase where the ticket workflow is being graded.
