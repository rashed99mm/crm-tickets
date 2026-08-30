# Task 1 — The ticket API service

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — task 2.1 |
| Feature | `FEAT-04` Ticket capture (frontend) |
| Criteria | supports `AC-59`, `AC-60`, `AC-57` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `frontend/projects/common/src/lib/tickets/ticket.api.ts`
- `frontend/projects/common/src/lib/tickets/ticket.api.spec.ts`
- `frontend/projects/common/src/public-api.ts`

## Test evidence

`npx ng test common --watch=false` — **55 passed (14 files)**.

- `posts a create to /api/Tickets with the payload the backend expects` — method, URL and body
- `sends the filters as query parameters`
- `omits an unset status rather than sending an empty one`
- `unwraps the envelope so callers see the page, not the envelope`

## Deviations from the plan

**1. `listCategories()` was added mid-task.**
The plan listed `create`, `listCategories` and `searchCustomers`, but the backend had no categories
endpoint to call — see `FEAT-04` backend task 5. The method was written after that endpoint existed,
not before.

**2. `TicketPage` was declared here rather than reusing `PagedResult<T>`.**
`PagedResult<T>` in `api-response.ts` declares `page`; the server sends `pageIndex`. Confirmed
against a real response rather than guessed. Because `PagedResult<T>` is exported from the library's
public API and has no current consumer, editing it would change a published type to fix a consumer
that does not exist — so the correct shape was declared alongside it, with a comment pointing at the
stale one. **This leaves the divergence documented in two places instead of fixed in one, which is
worse**, and it is flagged for `FEAT-09`'s contract-hardening pass.

## The point of this task

**The service catches nothing.** Failures propagate as `ApiError` from the envelope interceptor. A
service that swallowed them — returning an empty page on failure, say — would be the first step
towards rendering a server outage as "no tickets", which is precisely what `AC-58` exists to
prevent. The place to decide what a failure looks like is the component's `AsyncState`, and there is
exactly one such place.

`omits an unset status rather than sending an empty one` looks like a triviality and is not: the
backend refuses an unrecognised status value with a 400 rather than matching nothing, so `status=`
as a blank string would turn "no filter" into a rejected request.
