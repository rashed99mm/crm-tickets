# Frontend foundation — Angular 21 workspace, shared library, Tailwind v4 design system

> **Superseded 2026-08-25 by the platform baseline.** The backend this document describes was
> replaced when the CCE Platform reference was adopted as the CRM baseline — see
> [`EPIC-12-US-000-crm-platform-baseline-design.md`](../specs/EPIC-12-US-000-crm-platform-baseline-design.md).
> The code named below no longer exists in `src/`; it is archived, not deleted. This file is kept
> because it is the record of what was built and why, and deleting it would erase the reasoning
> behind decisions that still hold — the envelope, the localisation approach and the dependency rule
> among them. **Do not follow its steps.**


**Date:** 2026-08-25
**Criterion ids:** this spec uses the **`FE-n`** prefix. S1 owns `AC-n`, the backend foundation owns
`FND-n`. Separate prefixes so no two documents ever collide.
**Relates to:** `EPIC-02-US-016-ticket-lifecycle.md` (`AC-55`..`AC-68` are the frontend criteria
this foundation must make satisfiable) · ADR 0004 (response envelope) · ADR 0007 (bilingual messages)
· ADR 0008 (two API hosts)

## Problem

The backend has a working foundation and an applied schema. There is no frontend at all — no
workspace, no build, no styling, no way to see any of it. S1 carries fourteen frontend acceptance
criteria (`AC-55`..`AC-68`) and none of them can be attempted until a workspace exists with the
response envelope, authentication, localisation and state conventions already settled.

Getting those conventions wrong is expensive in a specific way: the envelope shape, the RTL strategy
and the async-state model each touch every component written afterwards. They are cheap now and a
rewrite later.

## Decisions taken, with evidence

| Decision | Choice | Why |
|---|---|---|
| Angular major | **21.2** | Probed: Angular 22 requires Node `^22.22.3 \|\| ^24.15.0`; this machine runs **24.11.1**, so 22 is unavailable without a Node upgrade. 20 and 21 both accept `>=24.0.0`. |
| Change detection | **Zoneless** | Probed: `ng new` on 21.2 installs **no `zone.js` at all**. Zoneless is the default and needs no provider. |
| Test runner | **Vitest 4** + jsdom 28 | Probed: the `@angular/build:unit-test` builder with Vitest is what 21.2 scaffolds. Karma is gone. |
| Topology | Workspace with **two apps + one shared library** | Mirrors ADR 0008's split of AdminApi/CustomerApi. |
| Realtime | SignalR client, **inert unless configured** | No backend hub exists and no S1 criterion needs realtime. |
| Design system | **Command Center** | See below — the mockups contain two competing systems. |
| Styling | Tailwind **v4.3** CSS-first `@theme` | Logical properties make RTL free. |

## Assumptions

- **E1.** `admin-app` talks only to `AdminApi`; `portal-app` talks only to `CustomerApi`. Neither
  calls the other's host.
- **E2.** `portal-app` ships as a shell with no features in S1. The customer portal is slice S3.
  This is a deliberate cost of scaffolding both apps now.
- **E3.** No typed client is generated from OpenAPI yet, because only `/health` exists. Feature
  models are hand-written per feature until endpoints land, then generation replaces them. This is a
  temporary state, recorded as one.
- **E4.** Arabic UI strings are developer placeholders, consistent with the backend's `Resources.yml`.
  The mechanism is real; reviewed translation is S8.
- **E5.** Light theme only. The mockups configure `darkMode: "class"` but no screen renders in dark
  mode and no screen defines a complete second palette, so dark mode is unbuilt, not merely unstyled.

## Out of scope

Any feature screen — customer list, ticket queue, ticket detail, forms bound to real endpoints. This
delivers the workspace, the shared library, the design system and the shell. Also out: dark theme,
SSR/hydration, PWA, e2e infrastructure (Playwright arrives with the first real journey in `AC-64`),
and every later-slice screen.

---

## The design system comes from Command Center, and here is why that mattered

The mockups in `stitch_smart_support_ticketing_crm/` are **two competing concepts, not one system**.
Nine screens follow "Proton Precision" (`primary: #000000`); four follow "Command Center"
(`primary: #00288e`). They share a font stack and a token *naming* convention, which makes them look
related, but their semantic-colour philosophies conflict and cannot be merged.

**Command Center is chosen** because it actually implements status and priority colour — the single
thing a ticket queue exists to convey. Proton Precision's own design document promises amber and
green for priority in prose, but no Proton screen ever renders them: only Critical is red and
everything else is neutral grey. Choosing Proton would have meant inventing the semantic layer
anyway, from a system whose `rounded-full` resolves to 12px instead of a pill.

Where the documents and the code disagree, **the code wins**: both design documents state a radius
ramp one notch larger than every screen actually renders, and Proton's documented `#0F172A` primary
appears in no screen at all.

### Two mismatches between the design system and our domain — resolved

The extraction surfaced two places where the mockups' vocabulary does not match the domain that now
exists in the database. Both need a decision rather than a mapping.

**Status.** The domain has `New · Open · Pending · Resolved · Closed`. Command Center provides
`status-open · status-pending · status-resolved · status-escalated`. So two of our statuses have no
colour and one of theirs describes a state we do not have (escalation is S2).

| Domain status | Token | Value |
|---|---|---|
| `New` | `--color-status-new` | `#64748B` slate — **added.** A new ticket is unclaimed, not alarming; it must read as neutral-but-present. |
| `Open` | `--color-status-open` | `#4F46E5` |
| `Pending` | `--color-status-pending` | `#F59E0B` |
| `Resolved` | `--color-status-resolved` | `#059669` |
| `Closed` | `--color-status-closed` | `#94A3B8` muted slate — **added.** Deliberately the lowest-contrast chip: a closed ticket should recede. |
| _(none in S1)_ | `--color-status-escalated` | `#DC2626` — reserved for S2, defined but unused. |

**Priority.** The domain has `Low · Normal · High · Urgent`. Command Center provides
`critical · high · medium · low`. A naive one-to-one mapping puts **`Normal` on amber**, which is
wrong: most tickets are Normal, and a queue where the default state reads as a warning trains agents
to ignore the colour entirely. The mapping shifts by one step instead:

| Domain priority | Colour | Value |
|---|---|---|
| `Low` | emerald | `#10B981` |
| `Normal` | slate | `#64748B` — **neutral on purpose.** The common case must not shout. |
| `High` | amber | `#F59E0B` |
| `Urgent` | deep red | `#B91C1C` |

`#EF4444` (the mockups' `priority-high`) is dropped; two reds one step apart are not distinguishable
at badge size, which defeats the purpose.

**One more conflict resolved:** Command Center defines both `background: #f8f9ff` and
`surface: #F8FAFC` — two near-identical near-whites, with its own prose calling Layer 0 "Slate 50"
(`#F8FAFC`). Two indistinguishable tokens for the same role is a trap. **`#F8FAFC` is the page
background**; the duplicate is not carried forward.

### What the mockups do not contain

Confirmed absent across all thirteen screens: **login, error state, empty state, loading state, form
validation display, language switcher, modal/dialog, toast, and disabled states.**

That is more than half of S1's frontend criteria — `AC-55` (login), `AC-58` (loading/empty/error
distinct), `AC-59`/`AC-60` (validation and field errors), `AC-68` (language switching). These are
**designed here from the extracted tokens**, and this spec is their source, not the mockups. Saying
so matters: an assessor comparing screens to mockups should know which parts were derived and which
were authored.

---

## Workspace layout

```
frontend/                                   Angular 21 workspace
  projects/
    common/                                 shared library — the cross-app "common"
      src/lib/api/                          envelope types · unwrap interceptor · auth interceptor · ApiError
      src/lib/auth/                         token storage · session signals · functional guards
      src/lib/realtime/                     SignalR connection service
      src/lib/i18n/                         locale signal · dir/lang effect · message resolution
      src/lib/ui/                           button · input-field · badge · table · card · empty-state
                                            · error-state · loading-state · language-switcher
      src/lib/testing/                      fakes and harness helpers
    admin-app/src/app/
      layout/                               shell: 280px sidebar · 64px topbar
      features/{auth,tickets,customers}/    vertical slices, lazy-loaded
    portal-app/src/app/
      layout/  features/{auth,my-tickets}/  shell only in S1
  styles/theme.css                          Tailwind v4 @theme tokens
```

`common/` is a real Angular library rather than a folder because two applications cannot share a
directory. Each app keeps its own `features/` slices.

## The envelope interceptor is the load-bearing piece

Every backend response is `{ success, code, message: {ar,en}, data, errors[], traceId, timestamp }`.
**One interceptor** in `common/api` unwraps it:

- success → the response body becomes `data`, so services return plain typed models
- failure → throws a typed `ApiError` carrying `code`, both language messages, `errors[]` and
  `traceId`

Services and components never see `success`. This is the only place in the frontend that knows the
envelope exists, and it is what makes `AC-60` — server field errors landing on their matching form
controls — possible at all. A second place that unwraps the envelope is a defect.

## Localisation and RTL

A `locale` signal (`'ar' | 'en'`) is the single source. It drives which half of every `message`
renders, and an effect sets `documentElement.lang` and `dir`. **Switching language never refetches** —
that is the entire reason ADR 0007 puts both languages in every response.

RTL is a clean slate and must be built correctly from the first component. The mockups contain
**121 physical-direction utilities and zero logical ones**, so none of their layout code transfers
directly. Rules:

- Logical properties only — `ps-*`/`pe-*`, `ms-*`/`me-*`, `start-*`/`end-*`, `border-s`/`border-e`,
  `text-start`/`text-end`. **No `left`/`right`/`pl-`/`pr-`/`ml-`/`mr-` anywhere.** A lint rule
  enforces this, because one physical utility is invisible until someone switches to Arabic.
- Logical corner utilities for chat-bubble tails, so the pointed corner follows the sender under both
  directions.
- Ticket references and timestamps stay LTR inside RTL text — wrap in `dir="ltr"` with
  `unicode-bidi: isolate`. `#TKT-000123` reversed is unreadable.
- Pagination chevrons **swap meaning**, not just position, under RTL. Dropdown chevrons are
  rotation-symmetric and safe.
- Arabic line-height is increased per Command Center's guidance, scoped to `:lang(ar)`.

## Async state

Signals throughout, with async modelled as a discriminated union:

```
idle | loading | loaded(data) | empty | error(ApiError)
```

Never "data or nothing". `AC-58` requires loading, empty and error to be visually distinct, and
`catchError(() => of([]))` is the default mistake that renders a server failure as "no results" —
the user then reports "there are no tickets" and nobody looks for the real fault. `EmptyStateComponent`
and `ErrorStateComponent` exist in `common/ui` so the distinction is structural rather than
remembered.

## SignalR

`RealtimeService` exposes a `connectionState` signal, takes its hub URL from environment config,
applies a reconnect policy, and is **inert when no URL is configured** — which is the state today,
because no backend hub exists. Unit-tested against a fake `HubConnection` so the reconnect and
state-transition logic is covered without a server.

## Testing

Vitest + jsdom, per what Angular 21 scaffolds. Coverage that matters at this stage:

- the envelope interceptor: success unwrapping, failure mapping, `errors[]` preserved, `traceId` kept
- `ApiError` field errors reachable by field name (this is what `AC-60` will bind to)
- the locale signal: switching flips `lang` and `dir` and **issues no HTTP request**
- async-state union: empty is distinguishable from error, and a failed request never yields empty
- `RealtimeService`: no-op when unconfigured, state transitions against a fake connection
- one component test per `common/ui` component, queried by accessible name rather than CSS class

## Acceptance criteria

- **FE-1** (P0) `frontend/` is an Angular 21 workspace holding `common`, `admin-app` and
  `portal-app`. All three build, and `npm test` runs green.
- **FE-2** (P0) `Domain`-style layering holds in reverse: `admin-app` and `portal-app` depend on
  `common`; `common` depends on neither app.
- **FE-3** (P0) A single interceptor unwraps the envelope. A success response yields `data`; a failure
  throws `ApiError` carrying code, both messages, `errors[]` and `traceId`.
- **FE-4** (P0) No component or feature service references `success` or `code` from a raw response.
- **FE-5** (P0) `ApiError` exposes field errors keyed by field name, camelCase, matching the request
  DTO.
- **FE-6** (P0) A `locale` signal drives message language, `documentElement.lang` and `dir`.
- **FE-7** (P0) Switching locale issues **no HTTP request**, asserted by a test.
- **FE-8** (P0) No physical-direction utility appears in any template; a lint rule fails the build if
  one does.
- **FE-9** (P0) Async state is a union with distinct `empty` and `error` members; a failed request
  never produces `empty`.
- **FE-10** (P0) Tailwind v4 `@theme` carries the Command Center tokens, including the six status and
  four priority colours resolved above.
- **FE-11** (P1) `common/ui` provides button, input-field (with validation display), badge, table,
  card, empty-state, error-state, loading-state and language-switcher.
- **FE-12** (P1) The `admin-app` shell renders a 280px sidebar and 64px topbar and survives an RTL
  flip without layout breakage.
- **FE-13** (P1) `RealtimeService` is inert with no configured hub URL and exposes a
  `connectionState` signal.
- **FE-14** (P1) `frontend/README.md` documents how to run each app, the folder conventions, and the
  envelope/locale/state rules.
- **FE-15** (P2) A `portal-app` shell builds and serves with no features.

## Which S1 criteria this foundation makes satisfiable

This spec builds no feature screen, so it satisfies no `AC-n` outright. What it does is make each one
reachable. Stating the mapping explicitly, because "the foundation is done" is otherwise unfalsifiable.

| S1 criterion | What this foundation provides | Still needed |
|---|---|---|
| `AC-55` login | Login screen designed here (no mockup existed), `common/auth` session signals | The auth endpoint and its handler |
| `AC-56` unauthenticated redirect | Functional `CanActivateFn` guards in `common/auth` | Route wiring per feature |
| `AC-57` paged list, filters, my-tickets | `table` component, async-state union, paged envelope type | The ticket list feature |
| `AC-58` loading/empty/error distinct | **`FE-9`** union plus the three state components | Use them per screen |
| `AC-59` client validation mirrors server | `input-field` with validation display, designed here | Per-form validators |
| `AC-60` server field errors on controls | **`FE-5`** — `ApiError` keyed by camelCase field name | Per-form binding |
| `AC-61` detail with guarded actions | `badge`, `card`, role signals from `common/auth` | The detail feature |
| `AC-63` no hardcoded strings, `dir` from locale | **`FE-6`**, **`FE-8`** — locale signal, dir effect, lint rule banning physical utilities | Per-template discipline, enforced by the lint rule |
| `AC-64` Playwright journey | Nothing — deliberately deferred until a real journey exists | The whole E2E setup |
| `AC-68` switch language without refetch | **`FE-7`**, asserted by test | Nothing |

`AC-62` and `AC-65` (notes and attachments UI) are S1 steps 7–8, already the agreed first cuts.

## Decisions to record as ADRs

- **ADR 0009** — Command Center over Proton Precision, with the status/priority remapping and the
  reasoning that the code outranks the design prose.
- **ADR 0010** — one shared library plus two applications, mirroring ADR 0008.
- **ADR 0011** — the envelope is unwrapped in exactly one interceptor.

## Build order

1. Workspace, three projects, Tailwind v4 wired, all building — **FE-1, FE-2**
2. `@theme` tokens and the RTL lint rule — **FE-8, FE-10**
3. Envelope types, `ApiError`, interceptors — **FE-3, FE-4, FE-5**
4. `locale` signal, dir/lang effect — **FE-6, FE-7**
5. Async-state union and the three state components — **FE-9**
6. Remaining `common/ui` components — **FE-11**
7. `admin-app` shell, RTL-verified — **FE-12**
8. `RealtimeService` — **FE-13**
9. Docs, skill updates, `portal-app` shell — **FE-14, FE-15**

Steps 1–5 are not cuttable: every feature screen depends on the envelope, locale and state
conventions. Steps 8 and 9 are the cut candidates.

## Existing project skills that are now wrong

Two skills I wrote earlier in this project describe a stack that no longer matches and would mislead
an implementer:

- `.claude/skills/angular-frontend/SKILL.md` says Angular 20, knows nothing about Tailwind, the two
  apps, or that zoneless is the default rather than an opt-in.
- `.claude/skills/angular-testing/SKILL.md` describes Karma/Jasmine as the likely runner. Angular 21
  ships Vitest and does not install Karma at all.

Both are updated as part of this work.
