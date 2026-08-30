# MVP-12 — Agent dashboard · **frontend** implementation plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Date:** 2026-08-26
**Spec:** [`../../specs/EPIC-08-US-606-agent-dashboard.md`](../../specs/EPIC-08-US-606-agent-dashboard.md)
**Criteria:** `AC-77`…`AC-82`
**Depends on:** the backend plan's `unassigned` flag for `AC-82` only. Everything else works today.

## Code plan

### T1 — API surface

**Edit:** `frontend/projects/common/src/lib/tickets/ticket.api.ts`

```ts
export interface TicketFilters {
  page?: number; pageSize?: number;
  status?: TicketStatus | null;
  mine?: boolean;
  unassigned?: boolean;      // AC-82
}
```

Send `unassigned` only when true, exactly as `mine` and `status` are handled — a blank query
parameter is not the same as an absent one, and the server refuses unrecognised filter values.

Add a small helper so the component does not repeat itself:

```ts
/** Total matching a filter, without pulling the rows. pageSize=1 because only totalCount is read. */
countOnly(filters: TicketFilters): Observable<number> {
  return this.list({ ...filters, page: 1, pageSize: 1 }).pipe(map((p) => p.totalCount));
}
```

### T2 — The dashboard component

**New:** `admin-app/src/app/features/dashboard/dashboard.component.{ts,html,spec.ts}`

Four independent `AsyncState` signals, so one failing count does not blank the list:

```ts
readonly myWork     = signal<AsyncState<TicketPage>>(loading());   // AC-77
readonly counts     = signal<AsyncState<StatusCounts>>(loading()); // AC-78
readonly unassigned = signal<AsyncState<number>>(idle());          // AC-82, supervisor only

readonly isSupervisor = computed(
  () => this.session.hasRole('Supervisor') || this.session.hasRole('Admin'));
```

- **`AC-77`** — `list({ mine: true, pageSize: 10 })`, newest first as the server returns it. Do not
  re-sort client-side; the ordering is a server guarantee and re-sorting would hide a regression.
- **`AC-78`** — `forkJoin` of three `countOnly({ mine: true, status })` for `New`, `Open`, `Pending`.
  `A17`: `Resolved` and `Closed` are not "my open work".
- **`AC-82`** — `countOnly({ unassigned: true })`, requested **only** when `isSupervisor()`. An agent
  must not issue the request at all; a hidden-but-issued request is still an information leak in the
  network tab.

### T3 — Template

- Count tiles for New / Open / Pending, then the list.
- Each row links to `/tickets/:id` (`AC-79`).
- **`AC-80`** — `CsEmptyState`, which carries **no retry**; **`AC-81`** — `CsErrorState`, which does.
  That presence/absence is both the honest signal and the visual difference.
- **`AC-82`** — the unassigned tile renders only for a supervisor and links to
  `/tickets?unassigned=true`.

Logical-direction utilities only. `rtl-safety.spec.ts` will fail the build otherwise.

### T4 — Routing

**Edit:** `app.routes.ts` — add `dashboard` inside the guarded shell and change the shell's default
child redirect from `tickets` to `dashboard`.

**Edit:** `login.component.ts` — the post-sign-in destination becomes `/dashboard`.
**Check:** `login.component.spec.ts` asserts `navigateByUrl('/tickets')`; that assertion must move to
`/dashboard`, and its sibling "returns the user to the url the guard interrupted" must still pass.

**Edit:** `shell.component.ts` — a "Dashboard" nav item, first.

## Tests

| Test | Criterion |
|---|---|
| `AC77: shows my open tickets without setting a filter` | `AC-77` |
| `AC78: shows a count for New, Open and Pending` | `AC-78` |
| `AC79: a row links to that ticket's detail` | `AC-79` |
| `AC80: no assigned work renders the empty state, with no retry` | `AC-80` |
| `AC81: a failed load renders the error state with a retry` | `AC-81` |
| `AC82: a supervisor sees the unassigned count` | `AC-82` |
| `AC82: an agent does not see it, and does not request it` | `AC-82` |

The last asserts `http.expectNone(...)` for the unassigned call, not merely that the tile is absent.

## Definition of done

`AC-77`…`AC-82` each covered by a test naming it · both `ng test` projects green with output pasted ·
`ng build admin-app` clean · task records in `tasks/`. **Do not touch `backend/`.**
