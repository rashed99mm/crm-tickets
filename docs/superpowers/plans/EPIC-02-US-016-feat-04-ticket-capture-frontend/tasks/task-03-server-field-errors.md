# Task 3 — Land server errors on the control that caused them

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 2.5, 2.6 |
| Feature | `FEAT-04` Ticket capture (frontend) |
| Criteria | `AC-60` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.ts` (`fieldError`, `formLevelError`, `clearServerError`)
- `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.html`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-create.component.spec.ts`

## Test evidence

- `AC60: a server field error appears under the control it names, not in a banner`
- `AC60: a failure naming no field renders at form level, since no control owns it`
- `clears a server field error once the user edits that control`

`npx ng test admin-app --watch=false` — **41 passed**.

## The test that proves the whole feature

`AC60: a server field error appears under the control it names` asserts **position**, not presence.
It finds the input carrying `aria-invalid="true"`, reads its `aria-describedby`, resolves that
element, and asserts the server's message is inside it — then asserts `formLevelError()` is null, so
the same text is not *also* dumped into a banner.

A test that merely checked the message appeared *somewhere* in the DOM would pass with every error
rendered as a banner, which is the exact arrangement `AC-60` forbids.

This is why `FEAT-04` was sequenced first among the vertical features, and **the contract held**: the
backend emits `details: { "Subject": [...] }`, the envelope interceptor lowercases the first
character to `subject`, and `ApiError.fieldError('subject')` returns it unchanged. The one thing that
had to be verified end to end verified clean.

## Deviations from the plan

**1. `clearServerError` was added, and no criterion asked for it.**
A corrected field that keeps displaying the rejection it already fixed makes the form look broken.
It is ordinary correctness rather than scope creep, and it is called out because it is code with no
`AC-n` above it.

**2. Nothing was built here that already existed.**
`CsInputField` already implemented the two-rule display — client errors after touch or dirty, server
errors immediately — and `ApiError.fieldError` already existed and was tested. Both were composed
rather than reimplemented. A second error-display path would have put two definitions of "what a
rejection looks like" in the codebase, and they would have drifted.

## The point of this task

`AC-59` and `AC-60` are separate criteria because their display timing differs, and no task should
"simplify" them into one rule:

- A **client** error shows only once the control is touched or dirty. A form the user has not filled
  in should not be a wall of red.
- A **server** error shows immediately. The request was already rejected; hiding the reason until the
  user happens to focus that particular field is worse than useless.
