# Frontend

Angular 21 workspace for the Customer Support CRM. Two applications over one
shared library, mirroring the backend's split into `AdminApi` and `CustomerApi`
(ADR 0008).

**Spec:** `../docs/superpowers/specs/2026-08-25-frontend-foundation-design.md`

## Projects

| Project | What it is | Talks to |
|---|---|---|
| `projects/common` | Shared library: API envelope handling, auth, i18n, realtime, presentational components | — |
| `projects/admin-app` | Staff application — agents and supervisors | `AdminApi` |
| `projects/portal-app` | Customer portal. **Shell only** — features are slice S3 | `CustomerApi` |

`common` is a real Angular library rather than a folder, because two
applications cannot share a directory. Each app keeps its own `features/`
vertical slices.

Note the workspace `tsconfig.json` maps `common` to
`projects/common/src/public-api.ts` rather than the built `dist/common`. That
is deliberate: apps compile the library from source, so no rebuild is needed
before running an app or its tests. It costs the ability to verify the packaged
output, which does not matter for a library that is never published.

## Commands

```bash
npx ng serve admin-app                 # run the staff app
npx ng serve portal-app                # run the customer portal shell

npx ng test common --watch=false       # library tests (most of the suite)
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false

npx ng build admin-app                 # production build
npx ng build common                    # build the library as a package
```

Tests run on **Vitest 4 + jsdom**, not Karma — Angular 21 does not install
Karma at all. Angular 21 is also **zoneless by default**: `zone.js` is not a
dependency and no change-detection provider is needed. All state is signals,
which is the mechanism rather than a preference.

## The four rules that matter

Break any of these and the damage spreads across every screen written
afterwards, which is why they are conventions rather than suggestions.

**1. The envelope is unwrapped in exactly one place.** Every backend response
is `{ success, code, message: {ar,en}, data, errors[], traceId, timestamp }`.
`envelopeInterceptor` unwraps it: success bodies become `data`, failures become
a typed `ApiError`. **No component or feature service may read `success` or
`code` from a raw response.** A second unwrapping point means two definitions
of "what failure looks like", and they will drift.

`ApiError.fieldError(name)` returns the server's error for one form control,
which is how a rejected request lands on the input that caused it.

**2. Logical properties only.** Use `ps-`/`pe-`, `ms-`/`me-`, `start-`/`end-`,
`border-s`/`border-e`, `text-start`/`text-end`. **Never** `pl-`, `pr-`, `ml-`,
`mr-`, `left-`, `right-`, `text-left`, `text-right`.

A physical utility breaks RTL *silently* — the layout looks right in English
and mirrors wrongly in Arabic, so nobody notices until an Arabic speaker opens
the app. `projects/common/src/lib/testing/rtl-safety.spec.ts` fails the build
on a violation. It scans `.html` files only, so **a component with an inline
template escapes it and needs its own assertion**, as both shells have.

**3. Async state is a closed union, never "data or nothing".** Use
`AsyncState<T>` — `idle | loading | loaded | empty | error`. `empty` and `error`
are separate members on purpose, and `CsEmptyState` / `CsErrorState` keep them
visually distinct.

Never write `catchError(() => of([]))`. It turns a server failure into "no
results": the user reports that their tickets are missing, and nobody looks for
the real fault because the UI said there was nothing to show.

**4. Both languages arrive in every response, so switching never refetches.**
`LocaleStore` holds one signal; an effect sets `documentElement.lang` and
`dir`. Changing language re-renders from data already in hand. A test asserts
no HTTP request is issued — do not "improve" this by reloading with the new
locale, which is exactly what ADR 0007 exists to avoid.

## Design system

Tokens live in `projects/common/src/styles/theme.css` as Tailwind v4 `@theme`
custom properties, extracted from the **Command Center** mockups in
`../stitch_smart_support_ticketing_crm/`.

That mockup folder contains **two competing design systems**. Nine screens
follow "Proton Precision" (black primary, status and priority rendered as
neutral grey); four follow "Command Center" (blue primary with a real semantic
status/priority palette). Command Center was chosen because conveying status
and priority at a glance is the one job a ticket queue has. Where a mockup's
design document disagreed with its own code, the code was taken as truth.

**Parts of the UI were designed here, not extracted.** No mockup in the set
contains a login screen, an empty state, a loading state, an error state, form
validation display, a language switcher, a modal, or a toast. Those were
designed from the token set. If you are comparing the built UI against the
mockups, that is why some of it has no counterpart there.

Three source conflicts are resolved in `theme.css`, with the reasoning inline:
two indistinguishable near-white background tokens; two domain statuses (`New`,
`Closed`) the mockups never coloured; and a priority mapping that would have
put `Normal` on amber — making the most common state read as a warning and
training agents to ignore priority colour entirely.

## Known temporary states

Both are deliberate and recorded rather than forgotten.

- **No generated API client.** The api-contract rule says generate a typed
  client from OpenAPI, but only `/health` exists so far. Feature models are
  hand-written per feature until endpoints land, then generation replaces them.
  Two hand-maintained copies of one contract will drift, so this is a debt to
  repay, not a pattern to extend.
- **`portal-app` has no features.** The customer portal is slice S3. The app
  builds and serves a shell so the structure is real and the shared library is
  exercised by two consumers rather than one.

## What is not here

No feature screen. This is the foundation: the shell, the component library
and the conventions. Login, the ticket queue, the create form and ticket detail
are S1 frontend work, and each needs a backend endpoint that does not exist
yet.

No dark theme — the mockups configure `darkMode: "class"` but no screen renders
in it and none defines a complete second palette, so there was nothing to
extract. No SSR. No Playwright: end-to-end arrives with the first real journey,
`AC-64`.
