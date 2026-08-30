# ADR 0012 — Seed `Agent` and `Supervisor` alongside the platform's inherited roles

- **Status:** Accepted
- **Date:** 2026-08-26

## Context

`FEAT-07` cannot be built without deciding what a role is called. Four approved criteria name the
roles directly:

- **AC-42** — a **Supervisor** assigning a ticket to an agent gets 200.
- **AC-43** — an **Agent** assigning any ticket gets 403.
- **AC-44** — a target user who is not an **agent** is a 400.
- **AC-47** — a **Supervisor** may change the status of any ticket.

Assumption **A2** in the slice spec states it outright: "Two roles are sufficient: `Agent` (works
assigned tickets) and `Supervisor` (assigns and reassigns any ticket, manages customers)."
`erd.md` §6 agrees, and records a correction where two earlier mentions of "Team Lead/Manager" were
found to name roles that exist nowhere else in the documentation set.

The adopted platform disagrees, because it was written for a different product.
`IdentitySeeder.SeedRolesAsync` seeds six roles — `SuperAdmin`, `Admin`, `ContentManager`,
`StateRepresentative`, `User`, `Visitor` — and `AddPlatformAuthorization` defines policies over them.
Neither `Agent` nor `Supervisor` exists anywhere in the running system.

Day 1 did not need to resolve this: no criterion in `FEAT-03`, `FEAT-04` or `FEAT-05` restricts by
role, so those endpoints use the `Authenticated` policy and nothing was invented. `FEAT-07` is
entirely about role restrictions and cannot dodge it.

## Decision

Seed `Agent` and `Supervisor` **in addition to** the six inherited roles, and add two policies over
them. The inherited roles keep their current meaning and their current policies untouched.

`Supervisor` is granted wherever `Admin` is, so an administrator is not locked out of supervisory
actions — but `Admin` is **not** treated as an agent, because "can administer the platform" and
"works a support queue" are different claims and `AC-44` turns on the second one.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **Rename the inherited roles to `Agent`/`Supervisor`** | The cleanest end state — one vocabulary, no ambiguity. It loses because `Admin` is load-bearing today: `UsersController` is `[Authorize(Policy = "Admin")]`, the seeded administrator holds it, the Angular `users` route guards on it, and `ContentManager` gates the knowledge base. Renaming means a data migration over `AspNetRoles`/`AspNetUserRoles` plus edits across features this slice does not touch, to satisfy criteria that only need two names to exist. |
| **Map the spec's roles onto the inherited ones — `Admin` = Supervisor, `User` = Agent** | Zero new roles and no migration. Rejected because it makes every future reader translate: a test named `AC43_Agent_Cannot_Assign` that signs in a `User` is a test whose subject you cannot see. It also silently widens `AC-44` — every `User` on the platform, including non-support staff, becomes a valid assignment target. |
| **Use the `Authenticated` policy and enforce roles in handlers only** | Endpoint policies and in-handler checks are *both* required by the spec's design section, and `AC-43` is specifically an endpoint-level control. Dropping the policy layer would make the 403 depend on a handler branch nobody is forced to write. |
| **Defer and ask** | Considered, and rejected as a blocking question: the approved spec already answers it. `A2` is not ambiguous, and the platform's role list is an inherited artefact, not a requirement. Asking would have stalled the phase over a decision the specification had already made. |

## Consequences

**Easier.** `AC-42`–`AC-47` become directly expressible: a policy named `Supervisor`, a role named
`Agent`, and tests whose names describe their own fixtures. Nothing that currently works changes
behaviour.

**Harder.** The system now carries **eight roles across two vocabularies**, and a reader has to know
that `User`/`Admin` belong to the inherited platform surface while `Agent`/`Supervisor` belong to the
support domain. That is genuine conceptual debt, and it is the cost of not doing the rename. If the
knowledge-base and user-administration features are ever re-specified for this product, collapsing
the two vocabularies should be part of that work — it is not a change to make while the ticket
workflow is the thing being graded.

**Watch for.** A user with neither `Agent` nor `Supervisor` — including the seeded administrator
before roles are assigned — can authenticate and read the queue but cannot be assigned a ticket.
That is correct rather than a bug, and `AC-44` is the criterion that says so.
