# Task 2 — The create-ticket form and its client rules

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 2.2–2.4 |
| Feature | `FEAT-04` Ticket capture (frontend) |
| Criteria | `AC-59` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.html`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.spec.ts`

## Test evidence

`npx ng test admin-app --watch=false` — **41 passed (8 files)**.

- `AC59: does not submit while the form is invalid`
- `AC59: the submit button is disabled while the form is invalid`
- `AC59: rejects a subject over 200 characters before submitting`
- `AC59: does not submit twice while a request is in flight`

## Deviations from the plan

**1. The submit-disabled test was added late.**
`US-127`'s TC-03 asked for it and nothing covered it until the test-case table was reconciled against
reality. The behaviour existed (`[disabled]="form.invalid"`); only the proof was missing. Found by
reading the story's rows, not the code.

**2. The customer picker is a plain `<select>` over the first 20 customers, not a typeahead.**
The plan said "customer picker" without specifying. With more than 20 customers the right one may not
be in the list. A **known limitation, not an oversight** — a typeahead is half a day the two-day
budget does not have. Recorded so it is not mistaken for finished work.

**3. Picker load failures degrade to an empty list.**
If `/api/Categories` or `/api/Customers` fails, the select renders empty rather than showing an error.
Deliberate: a *load* failure is not a *submit* failure, and routing it through `submitError` would
claim the submission failed before anything was submitted. The honest treatment is a per-picker error
state, which is not built. Also a known limitation.

**4. Priority is a fixed `<select>` of the four valid values.**
So an invalid priority is unreachable from the UI, and `US-127` TC-01 is marked `PARTIAL` for that
reason rather than a test being invented that could only fail if the template were rewritten.

## The point of this task

Client rules **mirror** the server's — 200 characters because `CreateTicketCommandValidator` says
200, not because the input looked about right. Where the two disagree the server wins, and `AC-60`'s
path is what shows the user why. The mirroring is a courtesy that saves a round trip; it is not the
control, and treating it as one is how client-side-only validation gets shipped.
