# Task 3 — The guard that keeps `AC-63` true

| Field | Value |
|---|---|
| Plan | [`../implementation-plan.md`](../implementation-plan.md) — T4 |
| Story | `MVP-13` |
| Criteria | `AC-63` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `frontend/projects/common/src/lib/testing/no-hardcoded-strings.spec.ts` — new

## Test evidence

`AC63: every UI string resolves through the dictionary` — passing, inside the **80 passed, 0 failed**
`common` run.

**Proven non-vacuous.** A test that scans for something and finds nothing is indistinguishable from a
test whose regexes match nothing at all, so the guard was deliberately broken:

```
<p>Escalations awaiting review</p>   ← added to dashboard.component.html
```

```
FAIL  common  projects/common/src/lib/testing/no-hardcoded-strings.spec.ts
      > AC63: every UI string resolves through the dictionary
+   "dashboard/dashboard.component.html: Escalations awaiting review",
Test Files  1 failed | 16 passed (17)
      Tests  1 failed | 79 passed (80)
```

The line was then reverted and the suite is green again.

## Method

Strip everything that is *not* visible text, then assert nothing readable remains:

1. comments — they contain prose, which is what is being hunted
2. `{{ … }}` interpolations
3. tags, with every attribute and binding inside them
4. `@if` / `@for` / `@switch` headers, then their braces

Interpolations are stripped rather than inspected: `{{ 'x' | t }}`, `{{ customer.name }}` and
`{{ locale.resolve(m) }}` are all legitimate and telling them apart would need a parser.

## Ordering cost an iteration, and is now a comment in the file

The first version stripped interpolations **last**. The brace cleanup ran first and ate one closing
brace, leaving `{{ ticket.reference }` — which no longer matched the interpolation pattern. Every
interpolation in the repository was reported as an offender:

```
Tests  2 failed | 77 passed (79)
```

Interpolations are now stripped second, immediately after comments, with the reason written down.

## The allowlist

Two patterns, both punctuation-only:

```ts
/^[—–\-→,;:.?!()[\]{}|/\\]+$/    // separators between two interpolated values
/^['"…]+$/                       // a stray quote or ellipsis
```

These carry no language — the em dash between `{{ customer.name }}` and `{{ customer.email }}`, the
arrow in `{{ from }} → {{ to }}` — and would be identical in Arabic. **Anything a translator would
want to change does not belong there**, and every addition is a hole in the guard.

`index.html` is skipped: its `<title>` is read by the browser before Angular exists, so it cannot
come from a signal-backed dictionary.

## Why this test is the deliverable

Converting fifteen templates makes `AC-63` true today. Only the sweep makes it true on the sixteenth
screen. The failure mode without it is invisible: an English label in an Arabic page reads as a
missing translation rather than as a bug, so nobody files it, and the criterion decays a screen at a
time.

Same shape as `rtl-safety.spec.ts`, which already caught one `text-left`.

**Known limitation, shared with the RTL guard:** it scans `.html` only, so the four inline templates
(both shells, login, forbidden) escape it. Those carry their own assertions — see
`shell.component.spec.ts`.
