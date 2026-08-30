# Task 1 — The detail screen, its actions and its timeline

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — US-128, tasks 1.1–1.8 |
| Feature | `FEAT-06` frontend — closes `FEAT-07` and `FEAT-08`'s surface too |
| Criteria | `AC-61`, `AC-50`, `AC-58` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `frontend/projects/common/src/lib/tickets/ticket.api.ts` (`get`, `changeStatus`, `assign`, `listAssignableAgents`, `TicketDetail`, `PERMITTED_TRANSITIONS`)
- `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.{ts,html,spec.ts}`
- `frontend/projects/admin-app/src/app/app.routes.ts`, `app.config.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html` (rows link to detail)

## Test evidence

`npx ng test admin-app --watch=false` — **49 passed (9 files)**; `common` — **55 passed**;
`npx ng build admin-app` clean.

Eight tests, named for `AC-61`, `AC-58` and `AC-50`. Listed in the feature
[README](../README.md#criteria-delivered).

## The half of AC-61 that is not a control

`AC61: the assign action is hidden for an agent` is a **courtesy**. The security boundary is the
server's 403, proven in `FEAT-07` by `AC43_Agent_AssigningAnyTicket_Returns403` and
`AC43_Agent_AssigningTheirOwnTicket_StillReturns403`, and it holds whatever the browser renders.

The component comment and the template comment both say so, because a hidden control is exactly the
kind of thing a later reader mistakes for authorization.

## Deviations from the plan

**1. The permitted-transition table is duplicated into the client.**
`PERMITTED_TRANSITIONS` in `ticket.api.ts` mirrors the backend's `TicketStatus`. The status action
offers only what the current status permits, so it does not present a move that will bounce.

Two copies can drift, and that is accepted: a drifted client is a **worse experience, not a hole**.
An offered-but-forbidden transition still comes back 409, and the conflict test proves the screen
handles it. Same trade as `AC-59`'s mirrored validators, recorded the same way.

**2. `withComponentInputBinding()` was switched on globally.**
Needed for `input.required<string>()` to receive the route's `:id`. A router-wide change made for
one component — deliberate, and the alternative (injecting `ActivatedRoute` here) would have left
the next component to make the same choice again.

**3. The initial load runs in `queueMicrotask`, not an `effect`.**
The route input is not bound at construction time. An `effect` would also work but re-fires on
unrelated signal writes; the microtask runs the load exactly once, after binding. Unusual enough to
carry its reasoning in the component.

**4. A failed mutation re-reads while keeping the error on screen.**
Not in the plan's task list as a separate item. On a 409 the local `rowVersion` is stale by
definition, so patching the local copy would leave the screen holding a superseded version and the
next attempt would fail identically. `reloadPreservingError` re-reads and restores the message —
the user sees fresh data *and* why their action was refused.

## Known limitations

- **No optimistic UI.** Every action round-trips and then re-reads, so there is a visible pause.
  Correct rather than fast, which is the right order for a screen whose whole point is that
  concurrent edits are refused.
- **Timestamps render raw ISO strings.** No date formatting or relative times; `AC-63`'s
  localisation pass (`FEAT-10`, Phase 4) is where that belongs.
- **The assign picker does not show the current assignee** as pre-selected, only the available
  agents. Minor, and no criterion asks for it.
