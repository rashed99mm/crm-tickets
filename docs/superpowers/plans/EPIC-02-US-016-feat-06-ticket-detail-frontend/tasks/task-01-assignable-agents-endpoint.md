# Task 0 — The agents the picker needs · **a backend gap this frontend plan uncovered**

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — task 0 |
| Feature | `FEAT-06` frontend, filling a `FEAT-07` backend hole |
| Criteria | none directly; required by `US-128`, and reuses `AC-44`'s rule |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Interfaces/IIdentityUserService.cs` (`GetUsersInRoleAsync`)
- `src/CustomerSupport.Infrastructure/Services/IdentityUserService.cs`
- `src/CustomerSupport.Application/Features/Tickets/Queries/GetAssignableAgents/GetAssignableAgentsQuery.cs`
- `src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (`GetAssignableAgents`)

## Test evidence

- `AssignableAgents_ReturnsOnlyUsersInTheAgentRole`
- `AssignableAgents_AgentIsRefused` — 403, because enumerating staff is a supervisory surface

Suite: **233 passed, 0 failed.**

## Why this task exists

The assign control needs a list of agents to pick from, and **nothing could supply one**.
`/api/Users` exists but is `[Authorize(Policy = "Admin")]`, and ADR-0012 is explicit that a
supervisor is not an administrator — those are different claims, which is the whole reason `Admin`
is not treated as an agent for `AC-44`.

So `FEAT-07`'s backend plan delivered an assign endpoint that a real screen could not drive.

**This is the second time this exact defect has occurred** — `FEAT-04` seeded ticket categories and
exposed nothing, discovered when the create form needed a picker. Both times a backend plan failed
to derive an endpoint from the frontend story it names as its own counterpart, and both times the
feature loop caught it inside a day. It is now a pattern to check for when writing a backend plan,
not bad luck twice.

## Design decisions, since there was no plan for this

1. **Narrow on purpose.** Id, name and email — exactly what the picker renders. Widening the
   user-administration surface to supervisors would have been the lazier fix and would have handed
   them a list of every account on the platform.
2. **The same role filter the mutation enforces.** `AC-44` refuses a non-agent target, so the picker
   must not offer one; otherwise the UI presents choices the server rejects.
3. **Active users only.** A deactivated account is not someone work can be handed to, and offering
   one produces an assignment nobody ever works. Not required by any criterion — it is the kind of
   thing that only shows up in use.
4. **On the tickets controller, not a new one.** It exists to serve ticket assignment and nothing
   else; a `StaffController` would invite scope that no criterion asked for.

## Deviations from the plan

None — the plan was written *after* the gap was found, precisely so this task would have one.
