---
name: angular-frontend
description: Use when writing or reviewing any Angular code in this project - components, forms, state, routing, HTTP calls, interceptors, styling and error display for the Angular 21 zoneless + signals frontend
---

# Angular frontend

> **Corrected 2026-08-25.** This skill previously described Angular 20, treated
> zoneless change detection as an opt-in, and knew nothing about Tailwind or the
> two-application topology. All three were wrong in ways that would mislead an
> implementer. Verified against the real workspace:
>
> - **Angular 21.2.** Angular 22 needs Node `^24.15`; this machine runs 24.11.1.
> - **Zoneless is the DEFAULT.** `ng new` installs no `zone.js` and there is no
>   `polyfills` entry. Do not add `provideZonelessChangeDetection()` — it is
>   unnecessary. Signals are the mechanism, not a style choice.
> - **Two applications over one shared library** (`common`), mirroring ADR 0008.
> - **Tailwind v4** with CSS-first `@theme` tokens.
> - Tests are **Vitest** — see `angular-testing`.

## The workspace

`frontend/projects/common` is a shared Angular library; `admin-app` (staff) and
`portal-app` (customers) consume it. Shared code goes in the library, never
duplicated between apps. Each app owns its own `features/` vertical slices.

## Four rules that outrank everything else here

**The envelope is unwrapped in exactly one interceptor.** Every backend response
is `{ success, code, message: {ar,en}, data, errors[], traceId, timestamp }`.
`envelopeInterceptor` turns success into `data` and failure into a typed
`ApiError`. **Never read `success` or `code` in a component or feature service.**
`ApiError.fieldError(name)` is how a server rejection reaches its form control.

**Logical properties only.** `ps-`/`pe-`, `ms-`/`me-`, `start-`/`end-`,
`border-s`/`border-e`, `text-start`/`text-end`. Never `pl-`/`pr-`/`ml-`/`mr-`/
`left-`/`right-`/`text-left`/`text-right`. A physical utility breaks RTL
silently: correct in English, mirrored in Arabic, unnoticed until an Arabic
speaker opens the app. A test fails the build on violations, but it scans
`.html` files only — an inline template needs its own assertion.

**Async state is `AsyncState<T>`** — `idle | loading | loaded | empty | error`.
`empty` and `error` are distinct members. **Never `catchError(() => of([]))`**:
it renders a server failure as "no results", the user reports missing data, and
nobody looks for the real fault.

**Switching language never refetches.** Both languages arrive in every response
(ADR 0007). `LocaleStore` is one signal driving text, `lang` and `dir`.

## Overview

Angular 21.2, standalone components, signals for state. **Node 24.11.1 caps this at Angular 21** —
Angular 22 requires Node `^24.15`, so it will not install here.

No NgModules, no `zone.js`-era patterns. If a reference or an older answer suggests
`@NgModule`, `*ngIf` or constructor-injected `ActivatedRoute` snapshots, it predates this
setup — translate it rather than copying it.

## Structure

```
frontend/projects/
  common/src/lib/          SHARED LIBRARY - both apps import from here
    api/                   envelope types, ApiError, interceptors
    auth/                   session signals, guards
    i18n/                   locale signal, localize pipe
    state/                  AsyncState union
    realtime/               SignalR client
    ui/                     presentational components (cs- prefix)
  admin-app/src/app/
    layout/                 the shell
    features/<name>/        smart component, service, routes - lazy loaded
  portal-app/src/app/       same shape; no features until slice S3
```

Shared code goes in the library, never duplicated between apps. A feature reaching into another
feature's folder is a coupling problem — lift the shared piece into `common` instead.

## Components

- Standalone, with `changeDetection: ChangeDetectionStrategy.OnPush`.
- `inject()` for dependencies, not constructor parameters.
- Inputs via `input()` / `input.required()`, outputs via `output()`.
- Control flow with `@if` / `@for` / `@switch`, not the legacy structural directives. `@for`
  requires `track` — without a stable identity Angular destroys and rebuilds every row, which
  loses focus and scroll position.

**Keep templates free of logic.** A template calling a method to compute a value re-runs it on
every change detection pass. Use a `computed()` signal instead — it caches and only recomputes
when its inputs change.

Split smart from presentational. A component that both fetches data and renders detailed markup
is hard to test and hard to reuse; the fetching component should pass plain data down.

## State with signals

- `signal()` for local mutable state, `computed()` for anything derived, `effect()` sparingly.
- **Never write to a signal inside an `effect()` that reads it** — that is an infinite loop.
  Derived state is `computed()`, not an effect that assigns.
- Convert observables at the edge with `toSignal()`; keep signals in the template.
- Async state uses the `AsyncState<T>` union from `common`: `idle | loading | loaded | empty |
  error`. **Five members, not "data or nothing".** `empty` and `error` are separate because a
  component modelling only data-or-nothing shows an empty list when the request failed, which
  reads as "no results" and hides the bug.

## Forms

Typed reactive forms. Not template-driven — the assessment looks at validation, and reactive
forms make it explicit and testable.

**Frontend validation mirrors the server's rules; it does not replace them.** It exists for fast
feedback. The server is the trust boundary, always, because anyone can call the API directly.
When the two disagree the user sees a field accepted then rejected, so keep them in step and
derive both from the same acceptance criteria.

Requirements for every form:

- Validators matching the server: required, max length, ranges, patterns.
- Errors shown only after `touched` or `dirty`, so a pristine form is not a wall of red.
- Submit disabled while invalid *and* while in flight — a double-click that creates two records
  is a real bug and a common one.
- Server-side validation errors mapped onto the matching controls via
  `ApiError.fieldError(name)`, not dumped in a banner. That is why the envelope keys `errors[]` by
  field name. Use `CsInputField`, which already implements the touch-versus-server rule: a client
  error shows only after touch, a server error shows immediately.
- Every input has a label associated with it. A placeholder is not a label.

## HTTP and errors

- `provideHttpClient(withInterceptors([...]))`, functional interceptors.
- `authInterceptor` then `envelopeInterceptor`, in that order — the token must be on the request
  before the envelope interceptor handles the response. Both come from `common`.
- Feature services return typed models and call `HttpClient` directly for now. There is **no
  generated API client yet** — only `/health` exists to generate from. Hand-written models per
  feature are a temporary state to replace with generation once endpoints land, not a pattern to
  extend.
- **Never swallow an error into an empty result.** `catchError(() => of([]))` turns a failure
  into "no data" and makes the bug invisible. Surface it.
- Lazy-load feature routes with `loadChildren`. Guards as functional `CanActivateFn`.

## Accessibility and the visible flow

The rubric grades the *full feature flow*, which includes what happens when things are slow or
broken. Cover: a loading indicator, an empty state distinct from the loading state, a visible
error state with a way to retry, keyboard reachability, and focus moved sensibly after navigation
or dialog close.

## Red flags

| Thought | Reality |
|---|---|
| "I'll call the method in the template" | It re-runs every change detection pass. Use `computed()`. |
| "`catchError(() => of([]))` keeps the UI clean" | It hides failures as "no results". Model the error state. |
| "Frontend validation is enough" | Anyone can call the API directly. The server is the trust boundary. |
| "I'll show server errors in a toast" | Field errors belong on their fields. That is why they are keyed by field. |
| "`@for` without `track` is fine for a short list" | It rebuilds every row, losing focus and scroll. Always track. |
| "I'll write to the signal in the effect" | Infinite loop. Derived state is `computed()`. |
| "Loading and empty can share a template" | Then a failed request looks like no data. Three states. |
