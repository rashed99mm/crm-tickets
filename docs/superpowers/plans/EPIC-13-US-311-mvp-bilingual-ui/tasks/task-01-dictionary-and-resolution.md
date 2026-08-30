# Task 1 — The dictionary and how a template reaches it

| Field | Value |
|---|---|
| Plan | [`../implementation-plan.md`](../implementation-plan.md) — T1, T2 |
| Story | `MVP-13` |
| Criteria | `AC-63`, `AC-68` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `frontend/projects/common/src/lib/i18n/translations.ts` — new. 118 keys, both languages
- `frontend/projects/common/src/lib/i18n/locale.store.ts` — `t(key, ...params)`
- `frontend/projects/common/src/lib/i18n/translate.pipe.ts` — new. `{{ 'some.key' | t }}`
- `frontend/projects/common/src/lib/i18n/localize.pipe.ts` — `pure: false`, see below
- `frontend/projects/common/src/public-api.ts` — both exported
- `frontend/projects/common/src/lib/i18n/bilingual-ui.spec.ts` — new

## Test evidence

`npx ng test common --watch=false` — **80 passed, 0 failed** (71 before this story).

Naming their criteria:

- `AC63: every dictionary entry carries both languages`
- `AC63: the translate pipe re-renders text on switch`
- `AC63: a parameterised string keeps its value in both languages`
- `AC68: the localize pipe re-renders a server message on switch`
- `AC68: switching language issues no HTTP request`
- `AC68: a server message already on screen flips language from the response in hand`

## `as const satisfies`, and why

```ts
} as const satisfies Record<string, LocalizedMessage>;
export type TranslationKey = keyof typeof TRANSLATIONS;
```

`satisfies` checks every entry has both halves; `as const` keeps the literal key types, so
`TranslationKey` is a union and `{{ 'tickets.que.title' | t }}` is a **build error**. Without
`as const` the type widens to `string` and a typo becomes a blank label at runtime — visible only
to whoever opens that screen in that language.

## Parameterised entries

`{0}`, `{1}` are filled positionally by `t(key, ...params)`. Eleven entries use it — page summaries,
the removal confirmations, the attachment refusals, `Must be {0} characters or fewer`.

The alternative was to split the sentence around an interpolation in the template
(`Remove {{ name }}? This cannot be undone.`). That cannot be translated: Arabic puts the pieces in a
different order, and a translator editing `translations.ts` would never see the fragments. Keeping
the whole sentence in the dictionary is the point.

## The defect this task found: a pure pipe cannot be reactive

The plan asserted:

> It reads the locale signal, so Angular re-evaluates it on switch — no `pure: false` needed.

**That is wrong, and `AC63: the translate pipe re-renders text on switch` failed on it first run:**

```
AssertionError: expected 'Ticket queue' to be 'قائمة التذاكر'
```

Reading the signal inside `transform` does mark the view dirty, so the view refreshes — and then
`ɵɵpipeBind` sees the same key it saw last time and returns the **cached** string without calling
`transform` at all. A pure pipe is memoised on its arguments, and the locale is not one of them.

Both pipes are now `pure: false`. The cost is a dictionary lookup per change-detection pass on a
view that was already dirty.

`LocalizePipe` carried the identical mistake, and the identical comment claiming otherwise. Nothing
in either app used it yet — every screen calls `locale.resolve(...)` directly — so the defect had
never been observed. It has a test now (`AC68: the localize pipe re-renders a server message on
switch`).

## What is deliberately NOT in the dictionary

- **Server messages.** They arrive bilingual in the envelope (ADR-0007) and go through `resolve()`.
  That is the whole of `AC-68`, and putting them here would mean shipping a client copy of a
  catalogue the server owns.
- **The language switcher's `aria-label`.** It must be in the **target** locale, so an Arabic screen
  reader announces the Arabic option in Arabic. The dictionary resolves by the *active* locale, so
  routing it through `t()` would announce "Switch to Arabic" in English to the one user who cannot
  read it. Commented in place.
- **Reviewed Arabic copy.** The Arabic is developer placeholder text (`PA-7`), exactly as the backend
  catalogue's is. What ships is the mechanism.
