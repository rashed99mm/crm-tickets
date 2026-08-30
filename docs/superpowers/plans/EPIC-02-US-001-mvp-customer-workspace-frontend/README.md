# MVP-03 / MVP-04 / MVP-05 — Customer workspace · frontend task record

**Plan:** [`implementation-plan.md`](./implementation-plan.md)
**Executed:** 2026-08-26 by a subagent, working `frontend/` only, in parallel with the backend half
**Status:** delivered — `AC-69`…`AC-75`

## Evidence

```
npx ng test common    --watch=false → Test Files 15 passed (15) | Tests 64 passed (64)
npx ng test admin-app --watch=false → Test Files 13 passed (13) | Tests 88 passed (88)
npx ng build admin-app             → Application bundle generation complete
```

Baseline was 55 / 49. **+9 / +39.** Re-run independently after the agent reported; same numbers.

The single `NG04002` unhandled rejection in `admin-app` is the pre-existing one from
`shell.component.spec.ts` (it uses `provideRouter([])` and sign-out navigates to `/login`). Present
before this work, untouched.

## Files

**Created:** `common/src/lib/customers/customer.api.{ts,spec.ts}` ·
`admin-app/features/customers/customer-{list,create,detail,notes}.component.{ts,html,spec.ts}`

**Modified:** `common/src/public-api.ts` · `admin-app/app.routes.ts` (+ spec) ·
`admin-app/layout/shell.component.ts` (+ spec)

## Criteria delivered

| `AC-n` | Proving tests |
|---|---|
| AC-69 | list renders name/email/phone · failed load → error state, not empty · empty **search** says the search matched nothing · empty result has no retry · loading state · search refetches from page 1 · paging advances |
| AC-70 | server field error under the named control · duplicate email at **form level** · created customer lands on detail · `/customers/new` matches create, not detail · omitted phone sent as `null`, not `""` |
| AC-71 | renders profile + recorded-at · unknown customer → not-found state, not an empty form · **a server fault → error state with retry, not not-found** |
| AC-72 | save PUTs and re-reads · duplicate email → conflict at form level, change not applied · field-keyed rejection under its control |
| AC-73 | customer with tickets → refusal shown, stays on screen · no tickets → returns to list · **asking to remove sends no request until confirmed** |
| AC-74 | notes newest first with author and time · failed read → error state, not empty history · no notes → empty state · detail hosts the history |
| AC-75 | empty note sends **no request** (`expectNone`) · whitespace-only sends none · adding posts **only the body** and re-reads · rejected note shows the server message · no double-post in flight |
| AC-76 (client half) | `AC76: posting a note sends only the body` — asserts the whole request body equals `{ body: … }`, so a signature that gained an author would fail |

## Contract verification — done by me, not by either agent

The frontend agent flagged the real risk: it built the notes UI against the spec's fixed JSON while
the endpoint was being written in parallel, so **its tests would stay green even if the shipped
contract differed**. Checked once both halves existed:

| | Backend | Frontend |
|---|---|---|
| Read shape | `CustomerNoteDto(Id, Body, AuthorId, AuthorName, CreatedAt)` | `{ id, body, authorId, authorName, createdAt }` |
| Page shape | `PaginatedList<T>` → `items/pageIndex/pageSize/totalCount` | same — and it correctly uses `pageIndex`, not the stale `page` |
| Routes | `GET`/`POST /api/Customers/{id:guid}/notes` | identical |
| Write shape | `CreateCustomerNoteRequest(Body)` | posts `{ body }` |

**They match.** Parallel construction against a fixed contract worked, but only because the contract
was written down before either agent started.

## Deviations from the plan

**D1 — A 404 is not `empty()`.** The plan said "404 renders a not-found state". Reaching that by
setting `empty()` from an error callback would break the rule that `empty()` is reachable only from a
success path — the rule that stops an outage rendering as "no data". Instead `state` stays
`failed(error)` and the template branches on `error.status === 404`. `AC71: a server fault renders
the error state with a retry, not the not-found state` is the test that pins the distinction, and it
is a better test than the plan asked for.

**D2 — Delete confirmation is an in-component two-step, not `window.confirm`.** A native dialog
cannot be styled or translated through `LocaleStore`, and some embedded browsers suppress it —
silently turning a guarded action into an unguarded one.

**D3 — The post-save re-read does not pass through `loading`.** Setting `loading` would unmount the
notes child and re-issue its request on every profile save.

**D4 — Search fires on `change` (Enter/blur), not per keystroke.** No debounce utility exists in the
workspace and the plan did not ask for one; adding one would have been unspecified scope.

**D5 — One test renamed.** `AC72: saving a change puts it and re-reads` — the endpoint is a `PUT` and
the name now says so.

## Known gaps

- **No browser verification.** The charter asks for end-to-end-in-a-browser; the screens have only
  been exercised through `HttpTestingController`. The contract check above substitutes for it in
  part, but not for layout, focus or real latency. **Open.**
- **Strings are plain literals**, not i18n-resolved — a live gap against the charter's "no
  user-facing string is hardcoded", consistent with the existing ticket screens and closed by
  `MVP-13`.
