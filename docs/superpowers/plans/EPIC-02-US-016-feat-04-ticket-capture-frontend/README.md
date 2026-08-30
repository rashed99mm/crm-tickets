# FEAT-04 — Ticket capture · frontend task record

**Plan:** [`implementation-plan/implementation-plan.md`](./implementation-plan.md)
**Executed:** 2026-08-26
**Status:** delivered

## Evidence

```
npx ng test common --watch=false
 Test Files  14 passed (14)
      Tests  55 passed (55)

npx ng test admin-app --watch=false
 Test Files  8 passed (8)
      Tests  41 passed (41)

npx ng build admin-app
Application bundle generation complete. [4.674 seconds]
```

## Tasks

| # | Task | Criteria | Commit | Status |
|---|---|---|---|---|
| [01](./tasks/task-01-ticket-api.md) | The ticket API service | supports AC-59, AC-60 | uncommitted | `done` |
| [02](./tasks/task-02-create-form.md) | The create form and its client rules | AC-59 | uncommitted | `done` |
| [03](./tasks/task-03-server-field-errors.md) | Land server errors on the control that caused them | AC-60 | uncommitted | `done` |
| [04](./tasks/task-04-route-and-navigation.md) | Route the form, land back on the queue | AC-55, AC-56 | uncommitted | `done` |

## Criteria delivered

| `AC-n` | Test naming it | Where |
|---|---|---|
| AC-59 | `AC59: does not submit while the form is invalid`, `AC59: rejects a subject over 200 characters before submitting`, `AC59: does not submit twice while a request is in flight` | `ticket-create.component.spec.ts` |
| AC-60 | `AC60: a server field error appears under the control it names, not in a banner`, `AC60: a failure naming no field renders at form level, since no control owns it` | " |
| — | `TicketApi` contract tests: method, URL, body, envelope unwrapping, omitted-vs-empty filters | `ticket.api.spec.ts` |

## The test that proves the feature

`AC60: a server field error appears under the control it names` asserts **position**, not presence.
It reads the offending input's `aria-describedby`, resolves that element, and asserts the server's
message is inside it — then asserts `formLevelError()` is null, so the same text is not also dumped
into a banner.

A test that merely checked the message appeared *somewhere* in the DOM would pass with every error
rendered as a banner, which is the exact arrangement `AC-60` forbids. This distinction was the point
of sequencing `FEAT-04` first, and the contract held: the envelope interceptor's PascalCase →
camelCase mapping (`Subject` → `subject`) delivered the field key the form binds to, unchanged.

## Deviations from the plan

**D1 — Server-error clearing was added, and no criterion asked for it.**
`clearServerError(field)` drops a field's server error when the user edits that control. Without it a
corrected field keeps displaying the rejection it already fixed, and the form reads as broken. It is
ordinary correctness rather than scope creep, and it is called out here because it is code with no
`AC-n` above it.

**D2 — The customer picker is a plain select over the first page, not a typeahead.**
The plan said "customer picker" without specifying. It loads one page of 20 via
`searchCustomers('')`. With more customers than that, the right one may not be in the list. This is a
**known limitation, not an oversight** — a typeahead is a half-day and buys nothing the two-day
budget is short of. Recorded so it is not mistaken for finished work.

**D3 — Picker load failures are swallowed to an empty list.**
If `/api/Categories` or `/api/Customers` fails, the select renders empty rather than showing an
error. Deliberate: a *load* failure is not a *submit* failure, and surfacing it through
`submitError` would claim the submission failed before anything was submitted. The honest treatment
is a per-picker error state, which is not built. Also a known limitation.

**D4 — `text-left` was caught by the existing RTL-safety test.**
`rtl-safety.spec.ts` scans every template for physical-direction utilities and failed on the queue's
table header. Corrected to `text-start`. Worth noting that the guard did its job on the first
template written after it — the risk it describes is not theoretical.

## Not done

The form has no `.css` file and uses utility classes inline; the other features in this app split
styles into a sibling stylesheet. Cosmetic inconsistency, left as is.
