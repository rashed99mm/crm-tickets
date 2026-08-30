# MVP-13 — Bilingual UI · implementation plan

**Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.**

**Date:** 2026-08-26
**Story:** `MVP-13` in [`../../../requirements/mvp/epic-5-bilingual-platform.md`](../../../requirements/mvp/epic-5-bilingual-platform.md)
**Criteria:** `AC-63`, `AC-68` — approved in the ticket-lifecycle spec.
**Layer:** frontend only. The backend already sends `messageAr`/`messageEn` on every response.

## What already exists

`LocaleStore` (`frontend/projects/common/src/lib/i18n/locale.store.ts`) — `locale` signal, `direction`
computed, an `effect` setting `document.documentElement.lang`/`dir` and persisting to `localStorage`
under key `cs.locale`, `resolve(message)` picking the active half of a bilingual server message.
`rtl-safety.spec.ts` — fails the build on any physical-direction utility. `AC-68` was therefore
largely satisfied before this plan. What was missing: `AC-63` — every UI string was hardcoded English.

## Global Constraints

- New UI text goes through `TRANSLATIONS` + the `t` pipe, never a bare string literal, or Task 4's
  guard fails the build.

---

### Task 1: The dictionary (`AC-63`)

**Files:**
- Create: `frontend/projects/common/src/lib/i18n/translations.ts`

**Interfaces:**
- Produces: `TRANSLATIONS` (a `Record<string, LocalizedMessage>`), `TranslationKey = keyof typeof TRANSLATIONS`.

- [ ] **Step 1: Write the failing test** — covered by Task 4's sweep; no standalone unit test for
  the dictionary object itself, since its only job is to exist and type-check.

- [ ] **Step 2: Implement**

```ts
// frontend/projects/common/src/lib/i18n/translations.ts
import { LocalizedMessage } from '../api/api-response';

/**
 * Every user-facing string the CLIENT owns, in both languages, in one place.
 *
 * Server messages are deliberately NOT here — they arrive bilingual in the response envelope
 * (ADR 0007) and go through `LocaleStore.resolve()` / the `localize` pipe. This dictionary is only
 * for text no server ever sends: labels, buttons, headings, empty states, client-side validation.
 *
 * `{0}`, `{1}` … are substituted positionally by `LocaleStore.t(key, ...params)`.
 */
export const TRANSLATIONS = {
  'app.name': { en: 'Support Desk', ar: 'مكتب الدعم' },
  'nav.dashboard': { en: 'Dashboard', ar: 'لوحة العمل' },
  'nav.tickets': { en: 'Tickets', ar: 'التذاكر' },
  'tickets.queue.title': { en: 'Ticket queue', ar: 'قائمة التذاكر' },
  'action.cancel': { en: 'Cancel', ar: 'إلغاء' },
  'state.loading': { en: 'Loading', ar: 'جارٍ التحميل' },
  // … the full dictionary; every key below this line is added by the templates that need it
  // (Task 3), not invented up front — a dictionary entry with no consumer is dead weight the
  // sweep in Task 4 cannot even catch, since it only scans templates for what IS there.
} as const satisfies Record<string, LocalizedMessage>;

export type TranslationKey = keyof typeof TRANSLATIONS;
```

- [ ] **Step 3: Commit**

```bash
git add frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat(i18n): the client-owned string dictionary (AC-63)"
```

---

### Task 2: Resolution — `LocaleStore.t()` and `TranslatePipe` (`AC-63`, `AC-68`)

**Files:**
- Modify: `frontend/projects/common/src/lib/i18n/locale.store.ts`
- Create: `frontend/projects/common/src/lib/i18n/translate.pipe.ts`
- Test: `frontend/projects/common/src/lib/i18n/translate.pipe.spec.ts`

**Interfaces:**
- Produces: `LocaleStore.t(key: TranslationKey, ...params): string`; `TranslatePipe` (`{{ 'key' | t }}` / `{{ 'key' | t: param }}`).

- [ ] **Step 1: Write the failing test**

```ts
// translate.pipe.spec.ts
it('AC68: re-renders text on a locale switch', () => {
  const locale = TestBed.inject(LocaleStore);
  const pipe = new TranslatePipe();
  expect(pipe.transform('app.name')).toBe('Support Desk');

  locale.setLocale('ar');

  expect(pipe.transform('app.name')).toBe('مكتب الدعم');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test common --watch=false --include='**/translate.pipe.spec.ts'`
Expected: FAIL — `TranslatePipe` does not exist.

- [ ] **Step 3: Implement**

```ts
// locale.store.ts — add:
  t(key: TranslationKey, ...params: readonly (string | number)[]): string {
    const text = this.resolve(TRANSLATIONS[key]);
    return params.length === 0
      ? text
      : text.replace(/\{(\d+)\}/g, (whole, index: string) => {
          const value = params[Number(index)];
          // An unsupplied placeholder is left visible rather than blanked — a stray "{1}" on
          // screen is a bug report, a silently missing value reads as working software.
          return value === undefined ? whole : String(value);
        });
  }
```

```ts
// translate.pipe.ts
import { inject, Pipe, PipeTransform } from '@angular/core';
import { LocaleStore } from './locale.store';
import { TranslationKey } from './translations';

/**
 * `pure: false` is load-bearing. A pure pipe is memoised on its ARGUMENTS — reading the locale
 * signal inside `transform` marks the view dirty so it re-renders, but `ɵɵpipeBind` then sees the
 * same key it saw last time and hands back the cached string without calling `transform` again.
 * `AC63: the translate pipe re-renders text on switch` is exactly the test that would catch a
 * regression to `pure: true`.
 */
@Pipe({ name: 't', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly locale = inject(LocaleStore);

  transform(key: TranslationKey, ...params: readonly (string | number)[]): string {
    return this.locale.t(key, ...params);
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx ng test common --watch=false --include='**/translate.pipe.spec.ts'`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/projects/common/src/lib/i18n/locale.store.ts frontend/projects/common/src/lib/i18n/translate.pipe.ts frontend/projects/common/src/lib/i18n/translate.pipe.spec.ts
git commit -m "feat(i18n): LocaleStore.t() and the translate pipe (AC-63, AC-68)"
```

---

### Task 3: Convert every template (`AC-63`)

**Files:** every `.html` under `frontend/projects/admin-app/`, `portal-app/`, `common/` — `shell`,
`login`, `ticket-queue`, `ticket-create`, `ticket-detail`, `users`, `forbidden`, the `customers/*`
screens, `common/ui/*` (`CsEmptyState`, `CsErrorState`, `CsLoadingState`, `CsInputField`'s client
error strings).

**Interfaces:** none new — every template imports `TranslatePipe` and replaces a literal with
`{{ 'namespaced.key' | t }}`, adding the key to `translations.ts` (Task 1) as it goes.

- [ ] **Step 1–N**: one commit per screen (or a small related group), each: add the missing
  dictionary keys, replace the literal(s), re-run that screen's own `.spec.ts` to confirm nothing
  broke. Representative diff shape (`CsEmptyState`):

```html
<!-- before -->
<p>{{ message }}</p>
<!-- after -->
<p>{{ message }}</p> <!-- message is already caller-supplied and pre-translated; no change here — CsEmptyState never owned literal text -->
```

```html
<!-- ticket-queue.component.html, before -->
<h1>Ticket queue</h1>
<!-- after -->
<h1>{{ 'tickets.queue.title' | t }}</h1>
```

- [ ] **Final step: run the full suite, paste output**

Run: `cd frontend && npx ng test common --watch=false && npx ng test admin-app --watch=false && npx ng test portal-app --watch=false`
Expected: PASS, every screen's own spec green.

- [ ] **Commit** (per screen, as above; no single final commit needed since each step already committed).

---

### Task 4: The guard that keeps it true (`AC-63`)

**Files:**
- Create: `frontend/projects/common/src/lib/testing/no-hardcoded-strings.spec.ts`

**Interfaces:** none — a pure test file, no production code.

- [ ] **Step 1: Write the guard**

```ts
// no-hardcoded-strings.spec.ts
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, sep } from 'node:path';

/**
 * Method: strip everything that is NOT visible text — comments, tags with attributes/bindings,
 * `@if`/`@for`/`@switch` control-flow headers, `{{ … }}` interpolations — then assert nothing
 * readable is left.
 */
const ALLOWED: readonly RegExp[] = [
  // Separators and arrows between two interpolated values.
  /^[—–\-→,;:.?!()[\]{}|/\\]+$/,
  // A lone quote or ellipsis left by punctuation around an interpolation.
  /^['"…]+$/,
];

const SKIP = new Set(['node_modules', 'dist', '.angular', '.git']);
const SKIP_FILES = new Set(['index.html']); // document shell, not a component template

function htmlFilesUnder(dir: string): string[] {
  const found: string[] = [];
  for (const entry of readdirSync(dir)) {
    if (SKIP.has(entry)) continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) found.push(...htmlFilesUnder(full));
    else if (entry.endsWith('.html') && !SKIP_FILES.has(entry)) found.push(full);
  }
  return found;
}

function visibleText(template: string): string[] {
  const stripped = template
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/\{\{[\s\S]*?\}\}/g, '\n')
    .replace(/<[^>]*>/g, '\n')
    .replace(/@(if|else|for|switch|case|default|empty)\b[^{]*\{/g, '\n')
    .replace(/[{}]/g, '\n');
  return stripped.split('\n').map((l) => l.trim()).filter((l) => l.length > 0);
}

describe('hardcoded UI strings', () => {
  it('AC63: every UI string resolves through the dictionary', () => {
    const root = join(process.cwd(), 'projects');
    const offenders: string[] = [];
    for (const file of htmlFilesUnder(root)) {
      for (const text of visibleText(readFileSync(file, 'utf8'))) {
        if (ALLOWED.some((allowed) => allowed.test(text))) continue;
        offenders.push(`${file.split(sep).slice(-2).join('/')}: ${text}`);
      }
    }
    expect(offenders).toEqual([]);
  });
});
```

- [ ] **Step 2: Run to verify it passes against the now-converted templates**

Run: `cd frontend && npx ng test common --watch=false --include='**/no-hardcoded-strings.spec.ts'`
Expected: PASS, `offenders: []`.

- [ ] **Step 3: Commit**

```bash
git add frontend/projects/common/src/lib/testing/no-hardcoded-strings.spec.ts
git commit -m "test(i18n): guard against future hardcoded UI strings (AC-63)"
```

---

## What this plan does NOT deliver

**Reviewed Arabic copy.** The strings are developer-written placeholders (`PA-7`), same as the
backend catalogue. This delivers the *mechanism* — replacing them later is editing one file. See
`EPIC-13-US-313-reviewed-arabic` for that follow-on.

## Definition of done

`AC-63` and `AC-68` each covered by a test naming it · `npx ng test common --watch=false` and
`npx ng test admin-app --watch=false` green with output pasted · `npx ng build admin-app` clean ·
`rtl-safety.spec.ts` still green.
