# Epic 5 — Bilingual platform

**Brief area:** 12 (Platform) — Arabic & English, web and mobile friendly, multi-department,
multi-branch, custom branding

**MVP scope: language only.**

| Bullet | In MVP? | Why |
|---|---|---|
| Arabic & English | **Yes** — `MVP-13` | The product is for an Arabic-speaking market. An English-only support tool is unusable by half its intended operators |
| Web and mobile friendly | Already met | The screens use responsive layout; no story needed to restate it |
| Multi-department | **No** | Organisation structure is not modelled, and nothing in the MVP routes by department |
| Multi-branch | **No** | Same, and it changes every query's scope — a foundational change, not a feature |
| Custom branding | **No** | Theming is cosmetic and there is no second tenant to brand for |

---

## `MVP-13` — Use the system in Arabic or English

**As an** Arabic-speaking agent, **I want** the interface in my language, laid out correctly,
**so that** I can work without translating the tool in my head.

**Status:** `done` — 2026-08-26. `AC-63`, `AC-68`.

A 118-key dictionary, a `t(key, ...params)` resolver, a `TranslatePipe`, and **all 15 templates
converted** — none skipped. Guarded by `no-hardcoded-strings.spec.ts`, which I verified is
non-vacuous by injecting a literal and watching it fail by name.

**The Arabic is developer placeholder copy (`PA-7`).** This delivers the mechanism, not a translated
product. Replacing the strings later is editing one file.

### Two latent bugs this story exposed

**1. A pure pipe does not re-render on a signal change, and my plan said it would.** The plan
asserted the pipe "reads a signal, so Angular re-evaluates it on switch — no `pure: false` needed".
That is wrong: a pure pipe is memoised on its **arguments**. The signal read does mark the view
dirty and the view refreshes, but `ɵɵpipeBind` then returns the cached value without calling
`transform` at all. The test failed first run with
`expected 'Ticket queue' to be 'قائمة التذاكر'`.

**2. `LocalizePipe` carried the identical bug — and the identical wrong comment — unobserved.**
Nothing used it: every screen calls `locale.resolve()` directly, so a pipe that never re-rendered
had never been noticed. It has a test now.

### Deliberately not translated

Domain enum values the server sends as bare identifiers — `New`/`Open`/`Pending`, priorities, history
`changeType`. They render as **data**, not UI text. Translating them client-side would mean keeping a
second copy of a backend-owned vocabulary and blanking any value the server adds later. The honest
fix is to send them as `LocalizedMessage` from the backend, which is out of this story's scope and
recorded as such.

Also not translated: `index.html`'s `<title>` — the document shell, read before Angular exists.

### Acceptance criteria

1. Given I switch language, **every** user-facing string changes — labels, buttons, table headers,
   empty states, validation messages. No string is hardcoded in a template.
2. Given Arabic, the page direction becomes `rtl` and the layout mirrors correctly.
3. Given data already on screen, switching language **issues no new request**. The server sent both
   languages; re-fetching would throw that away.
4. Given a server message already displayed, switching language flips it between `ar` and `en` from
   **the response already in hand**.
5. Given I reload, my language choice survives.
6. No template uses a physical-direction utility (`ml-`, `text-left`, `border-l`).

### Notes

**Criterion 3 is the one that gets built wrong.** "Reload the page with the new locale" looks
reasonable, works, and quietly discards the entire reason the envelope carries two languages.

Criterion 4 is already possible: `LocaleStore.resolve()` picks the active half of a message and the
`localize` pipe wraps it. What is missing is the **UI string dictionary** — criterion 1.

Criterion 6 is already enforced: `rtl-safety.spec.ts` scans every template and fails the build on a
physical-direction utility. It has already caught one (`text-left` in the queue header).

### What this story does NOT deliver

**Reviewed Arabic copy.** The catalogue holds developer placeholders (`PA-7`). This story delivers
the *mechanism* — a dictionary, a switch, correct direction, no refetch — so that replacing the
strings later is editing one file rather than every template.

Saying otherwise would claim a translated product. It is not one, and a native speaker would see that
in a minute.
