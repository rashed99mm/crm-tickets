---
name: angular-testing
description: Use when writing or reviewing frontend tests - component tests with TestBed, HTTP tests with HttpTestingController, form validation tests, and the Playwright end-to-end test that covers the full feature flow
---

# Frontend testing

> **Corrected 2026-08-25.** This skill previously told you to "confirm the runner
> at scaffolding time" and described Karma/Jasmine as a live possibility. It is
> settled: Angular 21 ships **Vitest** and does not install Karma. The
> `TestBed`, `HttpTestingController` and query-by-accessible-name guidance below
> is unaffected and still applies.

## Overview

Two levels, with different jobs:

- **Component / service tests** — fast, isolated, HTTP faked. Cover logic, form validation, and
  the loading/loaded/error states.
- **Playwright E2E** — one real browser journey per feature through the real API. This is the
  artifact for the "Frontend & End-to-End Flow" criterion, and nothing else evidences it.

**The runner is Vitest 4 + jsdom 28**, via the `@angular/build:unit-test` builder. Verified
against the real workspace: Angular 21 installs **no Karma at all**. Do not add it, and do not
write Jasmine-specific APIs (`jasmine.createSpy`, `jasmine.objectContaining`) — use `vi.fn()` and
Vitest matchers instead.

`describe` / `it` / `expect` are Vitest globals and read identically, so `TestBed` guidance below
transfers unchanged. Two practical differences:

- `TestBed.tick()` does not exist on this version. Flush effects with
  `TestBed.inject(ApplicationRef).tick()`.
- A spec that needs Node APIs (a test walking the project tree, for instance) requires
  `@types/node` and `"node"` in that project's `tsconfig.spec.json` `types` array — the default
  spec context is jsdom with browser types only, and it will not compile otherwise. Scope it to
  the spec config so Node APIs stay unavailable to application code.

Run tests per project: `npx ng test common --watch=false`.

## Component tests

`TestBed` with the standalone component imported directly. Provide fakes for its injected
services.

What is worth testing:

- **Every member of the `AsyncState<T>` union**, not just the happy one: loading shows an
  indicator, loaded renders rows, empty says so without a retry, error shows a message WITH a
  retry. The error state is the one that gets skipped and the one that breaks in front of an
  assessor.
- **Empty distinct from error.** Assert they render differently — this is precisely the bug
  `catchError(() => of([]))` creates.
- Conditional rendering and disabled states.

Query by accessible name or role rather than CSS class where you can. A test bound to
`.btn-primary` breaks on restyling and passes when the button is unreachable by keyboard; a test
bound to the accessible name breaks only when behaviour actually changes.

Signals settle synchronously in most cases, but a template needs a change detection pass before
assertions. Use the harness's flush/detect step consistently rather than sprinkling waits.

## Form validation tests

Test the rules, not the framework. For each validation acceptance criterion:

- Invalid input marks the control invalid with the expected error key.
- The error message is **not** shown while the control is untouched and pristine.
- It *is* shown after touch.
- Submit is disabled while invalid, and while a submission is in flight.
- Server-returned field errors land on the matching control.

That last one is the highest-value test here: mapping the envelope's `errors[]` onto controls via
`ApiError.fieldError(name)` is custom code, easy to get subtly wrong, and invisible until a server
actually rejects something. `CsInputField` already implements it — test through that rather than
reimplementing the rule per form.

## HTTP tests

`HttpTestingController` — assert the request as well as the response. A test that only checks the
returned data passes when the service calls the wrong URL with the wrong verb and the fake
answers anyway.

Assert: method, URL including query parameters, and body. Then flush a response.

Cover the failure path too: flush a 400 carrying a realistic envelope with `errors[]` populated,
and a 500, then assert the service surfaces an `ApiError` rather than swallowing it. Always call
`verify()` in teardown so an unexpected extra request fails the test.

Also worth one test per feature service: that a locale change issues no request. Both languages
arrive in every response, so a refetch on switch means someone undid ADR 0007.

## Playwright E2E

One test per feature covering the whole journey a user takes — navigate, fill the form, submit,
see the result persisted, reload and confirm it is still there. That last step is what
distinguishes "the UI updated" from "the data was saved".

Include at least one negative journey: submit something invalid and assert the error is visible
to the user.

- Locate by role and accessible name (`getByRole`, `getByLabel`). Never by CSS class.
- Rely on Playwright's auto-waiting. **A fixed sleep is a flaky test with a delay** — it fails on
  a slow CI runner and wastes time on a fast one.
- Each test sets up its own data and does not depend on another test having run.
- Run against a real backend. An E2E with a mocked API tests nothing the component tests did not
  already cover.

## Running them

Run the suites and **paste the real output** before making any claim about them. A flaky test
reported as passing is worse than a failing one, because the next person trusts it.

## Red flags

| Thought | Reality |
|---|---|
| "I'll query by CSS class, it's stable" | It breaks on restyling and passes when the control is unreachable. Use role and accessible name. |
| "`waitForTimeout(2000)` fixes the flake" | It relocates the flake to a slower machine. Auto-waiting and explicit expectations. |
| "Mocking the API in E2E keeps it fast" | Then it tests nothing the component tests did not. E2E means the real thing. |
| "Testing the happy path in E2E is enough" | The negative journey is where the visible error state gets proven. |
| "I don't need to assert the request URL" | Then a call to the wrong endpoint still passes. |
| "The error state is hard to trigger in a test" | Flush a 500. It is the state most likely to be broken. |
