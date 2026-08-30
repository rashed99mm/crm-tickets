# Task 4 — The topbar claimed every screen was "Tickets"

| Field | Value |
|---|---|
| Plan | not in the plan — a defect found while converting the shell |
| Story | `MVP-13` (the file had to be touched anyway) |
| Criteria | `AC-63` for the labels; the routing half is a plain bug fix |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `frontend/projects/admin-app/src/app/layout/shell.component.ts`
- `frontend/projects/admin-app/src/app/layout/shell.component.spec.ts`

## Test evidence

`npx ng test admin-app --watch=false` — **115 passed, 0 failed** (113 before; the two new ones are
below). The pre-existing unhandled `NG04002 … 'login'` rejection from this spec file is unchanged and
unrelated — `signOut()` navigates to `/login`, which `provideRouter([])` cannot match.

New:

- `names the active screen rather than always saying Tickets` — asserts `/customers` → `Customers`,
  `/dashboard` → `Dashboard`, `/tickets` → `Tickets`, `/tickets/t-1` → `Tickets` (a child belongs to
  its parent screen), `/account/password` → `Password` (a longer path is not swallowed by a prefix)
- `AC63: the topbar heading comes from the dictionary, not a literal` — switches to Arabic and
  asserts the heading, the sidebar brand and the sign-out button all flip

## The bug

```html
<h1 class="truncate font-display text-headline-lg text-on-surface">
  Tickets
</h1>
```

A literal, on every screen. `/customers` and `/dashboard` both announced themselves as the ticket
queue. It survived because every routed component renders its own heading, so the screen was never
actually unlabelled — just labelled twice, once wrongly, in the larger of the two type sizes.

## The fix

One table, two readers:

```ts
const NAV_ITEMS: readonly { path: string; key: TranslationKey; adminOnly?: true }[] = [ … ];
```

`nav()` filters it for the sidebar; `title()` matches the active url against it. Two tables would
drift, and the drift would show up as exactly this bug again.

The url is a signal, because `router.url` alone is not reactive — a heading computed from it would
render once and then describe whatever screen happened to be first:

```ts
private readonly url = toSignal(
  this.router.events.pipe(
    filter((event): event is NavigationEnd => event instanceof NavigationEnd),
    map((event) => event.urlAfterRedirects),
  ),
  { initialValue: this.router.url },
);
```

The initial value covers the gap before the first `NavigationEnd`; without it the heading is blank on
the very first paint.

Two details worth stating:

- **Matching is longest-path-first**, so `/account/password` cannot be swallowed by a shorter prefix
  added above it later.
- **`/users` stays in the table even for a non-admin.** `adminOnly` hides the sidebar *link* (AUTH-22
  — the route guard and the endpoint policy are what actually refuse), but the heading must be right
  on any route that renders, and tying the two together would make a correct heading depend on the
  viewer's role.

Falls back to `app.name` on an unknown route. `/login` and `/forbidden` render outside this shell, so
nothing hits it today — but an empty topbar is a worse answer than the product name.
