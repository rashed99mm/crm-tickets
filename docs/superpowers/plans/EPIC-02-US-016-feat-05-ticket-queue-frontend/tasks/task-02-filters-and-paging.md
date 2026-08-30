# Task 2 — Status filter, "my tickets" toggle and paging

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — tasks 3.3–3.5 |
| Feature | `FEAT-05` Ticket queue (frontend) |
| Criteria | `AC-57`, part of `AC-58` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html`
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.spec.ts`

## Test evidence

- `AC57: the status filter refetches with the selected status`
- `AC57: the my-tickets toggle requests only the caller's own work` — also asserts **no** `assigneeId` is sent
- `AC57: advances the page parameter when the next page is requested`
- `says the filter matched nothing, rather than claiming the queue is empty`

`npx ng test admin-app --watch=false` — **41 passed**.

## Deviations from the plan

**1. The pagination test was added late.**
`US-038`'s TC-04 asked for it and nothing covered it until the test-case table was reconciled.
Second time in this feature that reading the story's rows found a real gap the code review had not —
the behaviour existed, the proof did not.

**2. Paging is previous/next, not a numbered pager.**
`hasMore()` derives from `page() * 10 < totalCount()`, with the page size **hardcoded to 10 in two
places** — the request and this calculation. Changing one without the other silently breaks the Next
button. A numbered pager reading `pageSize` from the response would be better and is not built.
A known limitation, recorded rather than left to be discovered.

**3. Changing a filter resets to page 1.**
Not in the plan, and necessary: staying on page 3 while narrowing a filter that now has one page of
results shows an empty page and reads as "no matches".

## The point of this task

**The empty-state copy differs by context**, and that is a piece of honesty rather than polish. "No
tickets have been raised yet" under an active filter tells the agent their queue is empty when it is
not. `emptyMessage()` switches to "No tickets match this filter" whenever a status or the mine toggle
is set. The fix is copy, not logic — which is exactly why it would have been easy to skip.

The toggle sends `mine=true` and nothing else. The caller id is resolved server-side from the token,
so the client has no assignee id to leak or to get wrong.
