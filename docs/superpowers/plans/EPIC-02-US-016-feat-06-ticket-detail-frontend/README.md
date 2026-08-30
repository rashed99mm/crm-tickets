# FEAT-06 — Ticket detail with guarded actions · frontend task record

**Plan:** [`implementation-plan/implementation-plan.md`](./implementation-plan.md)
**Executed:** 2026-08-26
**Status:** delivered. Closes the user surface of `FEAT-06`, `FEAT-07` and `FEAT-08`.

## Evidence

```
npx ng test common --watch=false      → Test Files 14 passed | Tests 55 passed
npx ng test admin-app --watch=false   → Test Files  9 passed | Tests 49 passed
npx ng build admin-app                → Application bundle generation complete
```

## Tasks

| # | Task | Criteria | Commit | Status |
|---|---|---|---|---|
| [01](./tasks/task-01-assignable-agents-endpoint.md) | **Unplanned backend gap** — the agents the picker needs | AC-42, AC-43 | uncommitted | `done` |
| [02](./tasks/task-02-detail-screen.md) | The detail screen, its actions and its timeline | AC-61, AC-50, AC-58 | uncommitted | `done` |

## Criteria delivered

| `AC-n` | Test naming it |
|---|---|
| AC-61 | `AC61: renders the customer summary, the history timeline and the status action`; `AC61: the assign action is hidden for an agent`; `AC61: the assign action is offered to a supervisor…`; `AC61: the status action offers only the transitions permitted from the current status`; `AC61: a status change echoes the rowVersion it read`; `AC61: assigning posts the agent id and the rowVersion`; `AC61: a conflict shows the server message and re-reads the ticket` |
| AC-58 | `AC58: a failed load renders the error state, not an empty ticket` |
| AC-50 | asserted inside the timeline test — actor display names present, raw ids absent |

## AC-61's two halves, and which one is the control

The criterion says the assign action is hidden for agents **and refused by the server if called
anyway**. Only the second half is a security control, and this record must not read as if the first
were:

- **Hidden** — `AC61: the assign action is hidden for an agent`, a component test. A courtesy, so
  people are not offered dead ends.
- **Refused** — `AC43_Agent_AssigningAnyTicket_Returns403` and
  `AC43_Agent_AssigningTheirOwnTicket_StillReturns403`, integration tests from `FEAT-07`. This is
  the control, and it holds whatever the browser renders.

`US-128` TC-03 records exactly this split.

## Deviations from the plan

**D1 — A second missing endpoint, same class of plan defect as `FEAT-04`'s categories.**
The assign picker needs a list of agents and nothing could supply one: `/api/Users` is Admin-only,
and ADR-0012 is explicit that a supervisor is not an administrator. `GET /api/Tickets/assignable-agents`
was added. **Second time a backend plan failed to derive an endpoint from the frontend story it
names as its counterpart** — the pattern is now worth watching for rather than treating as bad luck.

**D2 — `withComponentInputBinding()` was enabled on the router.**
The detail screen takes its id as an `input.required<string>()`, which needs route-parameter
binding. It was not enabled. Turning it on is a global router change affecting every route, made
deliberately rather than reaching into `ActivatedRoute` in one component.

**D3 — The permitted-transition table now exists in the client too.**
So the status action offers `Pending`/`Resolved` from `Open` rather than all five. The server holds
the same table and **remains the authority**; this is the same arrangement as `AC-59`'s mirrored
validators and carries the same drift risk. The mitigation is that a drifted client is a worse
experience, not a hole: an offered-but-forbidden transition still returns 409, which `AC61: a
conflict shows the server message and re-reads the ticket` covers.

**D4 — The initial load is queued on a microtask, not run in an `effect`.**
The route input is not bound at construction. An `effect` would work but would also re-fire on
unrelated signal writes; `queueMicrotask` runs the load exactly once, after binding. Slightly
unusual, so the reasoning is in the component.

## Not done

`US-128` TC-04 — the Playwright pass over the same visibility rules — belongs to `AC-64` / `FEAT-11`
in Phase 4 and is **not** claimed here.
