# US-312 RTL Layout — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Arabic workflows genuinely RTL across both Angular applications while keeping directional
meaning correct and non-directional icons unchanged.

**Architecture:** Logical CSS properties everywhere (already the shell's convention) plus a single
semantic icon-mirror rule (`[dir="rtl"] .rtl-mirror { transform: scaleX(-1); }`). No `left`/`right`/
`pl-`/`pr-`/`text-left`/`text-right` in any template or stylesheet. The `LocaleStore` already sets
`document.documentElement.dir`/`lang`, so the runtime wiring exists — this plan closes the markup and
icon gaps.

**Tech Stack:** Angular 20 standalone, signals, Tailwind v4, existing `CsIcon`/`LocaleStore`.

**Spec:** `docs/superpowers/specs/EPIC-13-EPIC-13-US-312-rtl-layout.md`

**Not implemented this pass.** This plan is written ahead of any code that implements it, per explicit
instruction — execution is a future session's work.

---

## Global Constraints

- The existing `rtl-safety.spec.ts` scans `.html` files only; both shells use **inline** templates, so
  they currently escape the scanner. Step 1 extends the scanner to `.ts` (inline `template:` / `templateUrl`
  is out of scope, but inline `template:` strings are in) and adds the four named assertions below.
- Directional icons that must mirror: chevrons, arrows, previous/next, breadcrumb separators. Non-
  directional and MUST NOT mirror: logos, `person`, `settings`, `support_agent`, `warning`, status dots.
  Reversing data arrays to fake direction is forbidden.
- Keep `US-313` out of this task — Arabic *copy* changes live there; this task only fixes *direction*.

---

### Task 1: Inventory + failing RTL tests (`AC-312.1`, `AC-312.2`)

**Files:**
- Modify: `frontend/projects/common/src/lib/testing/rtl-safety.spec.ts`
- Create: `frontend/projects/common/src/lib/i18n/locale.store.rtl.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/layout/shell.component.spec.ts`
- Modify: `frontend/projects/portal-app/src/app/layout/shell.component.spec.ts`

**Interfaces:**
- Consumes: `LocaleStore` (sets `dir`/`lang` via its constructor `effect`).

- [ ] **Step 1: Write the failing tests**

```ts
// frontend/projects/common/src/lib/i18n/locale.store.rtl.spec.ts
import { TestBed } from '@angular/core/testing';
import { LocaleStore } from './locale.store';

describe('LocaleStore RTL (AC-312.1)', () => {
  it('AC312_1_ArabicSetsHtmlDirAndLang', () => {
    const store = TestBed.inject(LocaleStore);
    store.setLocale('ar');

    expect(document.documentElement.dir).toBe('rtl');
    expect(document.documentElement.lang).toBe('ar');
  });

  it('AC312_1_EnglishResetsDirAndLang', () => {
    const store = TestBed.inject(LocaleStore);
    store.setLocale('en');

    expect(document.documentElement.dir).toBe('ltr');
    expect(document.documentElement.lang).toBe('en');
  });
});
```

```ts
// appended to the existing rtl-safety.spec.ts — extend the scanner to inline .ts templates
function tsFilesUnder(dir: string): string[] {
  const found: string[] = [];
  for (const entry of readdirSync(dir)) {
    if (SKIP.has(entry)) continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) found.push(...tsFilesUnder(full));
    else if (entry.endsWith('.ts')) found.push(full);
  }
  return found;
}

// inside the describe block, add:
  it('no inline .ts template uses a physical-direction utility', () => {
    const root = join(process.cwd(), 'projects');
    const offenders: string[] = [];
    for (const file of tsFilesUnder(root)) {
      const text = readFileSync(file, 'utf8');
      // only inspect inline template strings
      const templateBlocks = text.split(/template:\s*`/).slice(1);
      for (const block of templateBlocks) {
        const body = block.split('`')[0];
        for (const pattern of BANNED) {
          const hit = body.match(pattern);
          if (hit) offenders.push(`${file}: ${hit[0]}`);
        }
      }
    }
    expect(offenders).toEqual([]);
  });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test common --watch=false --include='**/rtl-safety.spec.ts' --include='**/locale.store.rtl.spec.ts'`
Expected: FAIL — `locale.store.rtl.spec.ts` doesn't exist yet; the inline scanner may also flag the
shell templates' existing `start-`/`end-` usages if mis-banned (they are logical, so they should NOT
flag — verify the BANNED list stays physical-only).

- [ ] **Step 3: Commit the tests (red) then implement in Task 2**

```bash
git add frontend/projects/common/src/lib/testing/rtl-safety.spec.ts frontend/projects/common/src/lib/i18n/locale.store.rtl.spec.ts
git commit -m "test(rtl): failing RTL inventory + dir/lang assertions (US-312 T1)"
```

---

### Task 2: Logical properties + semantic icon mirroring (`AC-312.1`, `AC-312.2`)

**Files:**
- Modify: `frontend/projects/common/src/styles/theme.css`
- Modify: `frontend/projects/admin-app/src/app/layout/shell.component.html`
- Modify: `frontend/projects/portal-app/src/app/layout/shell.component.html`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html` (pagination)
- Modify: `frontend/projects/portal-app/src/app/features/kb/kb-list.component.html` (pagination)
- Create: `frontend/projects/common/src/lib/testing/rtl-icon.spec.ts`

**Interfaces:**
- Consumes: `CsIcon` (already used), `LocaleStore.direction`.

- [ ] **Step 1: Add the semantic mirror rule to `theme.css`**

```css
/* frontend/projects/common/src/styles/theme.css — appended */
/* US-312: only ICONS whose glyph implies a direction may flip in RTL. Everything else uses
   logical properties (border-e, ps-, text-start, …) and needs no rule. */
[dir='rtl'] .rtl-mirror {
  transform: scaleX(-1);
}
```

- [ ] **Step 2: Apply `rtl-mirror` to directional icons, leave glyph-icons alone**

In both shell templates the collapse chevron already swaps glyph by state:
```html
<cs-icon [name]="collapsed() ? 'chevron_right' : 'chevron_left'" [size]="16" />
```
Add `class="rtl-mirror"` to it (a chevron pointing "next" must point to the inline-end in Arabic). The
brand mark, `support_agent`, `person`, `settings`, `menu`, `close` icons receive NO mirror class.

In `ticket-queue.component.html` and `kb-list.component.html` pagination:
```html
<button type="button" [disabled]="page() <= 1" (click)="prev()" aria-label="Previous page">
  <cs-icon name="chevron_left" class="rtl-mirror" />
</button>
<button type="button" [disabled]="page() >= totalPages()" (click)="next()" aria-label="Next page">
  <cs-icon name="chevron_right" class="rtl-mirror" />
</button>
```

- [ ] **Step 3: Sweep physical utilities out of every template**

Run the now-extended `rtl-safety.spec.ts`; for each offender replace with the logical equivalent:
`pl-4`→`ps-4`, `pr-4`→`pe-4`, `ml-2`→`ms-2`, `mr-2`→`me-2`, `text-left`→`text-start`,
`text-right`→`text-end`, `border-l`→`border-s`, `border-r`→`border-e`, `left-0`→`start-0`,
`right-0`→`end-0`, `rounded-tl-`→`rounded-ss-`, `rounded-tr-`→`rounded-se-`. Confirm the test goes
green with zero offenders across both `.html` and inline `.ts` templates.

- [ ] **Step 4: Write the semantic-mirror test**

```ts
// frontend/projects/common/src/lib/testing/rtl-icon.spec.ts
import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LocaleStore } from '../i18n/locale.store';
import { CsIcon } from '../ui/icon.component';

@Component({
  selector: 'test-host',
  imports: [CsIcon],
  template: `<cs-icon name="chevron_left" class="rtl-mirror" />`,
})
class Host {}

describe('AC312_2_DirectionalIconsMirrorOnlyWhenSemantic', () => {
  let fixture: ComponentFixture<Host>;
  let locale: LocaleStore;

  beforeEach(() => {
    fixture = TestBed.createComponent(Host);
    locale = TestBed.inject(LocaleStore);
  });

  it('mirrors a directional icon in Arabic, leaves it alone in English', () => {
    locale.setLocale('ar');
    fixture.detectChanges();
    const icon = fixture.nativeElement.querySelector('cs-icon');
    expect(getComputedStyle(icon).transform).not.toBe('none');

    locale.setLocale('en');
    fixture.detectChanges();
    expect(getComputedStyle(icon).transform).toBe('none');
  });
});
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd frontend && npx ng test common --watch=false --include='**/rtl-safety.spec.ts' --include='**/rtl-icon.spec.ts' --include='**/locale.store.rtl.spec.ts'`
Expected: PASS — no physical-direction offenders, Arabic sets `dir="rtl" lang="ar"`, and the directional
icon mirrors only under RTL.

- [ ] **Step 6: Commit**

```bash
git add frontend/projects/common/src/styles/theme.css frontend/projects/admin-app/src/app/layout/shell.component.html frontend/projects/portal-app/src/app/layout/shell.component.html frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.html frontend/projects/portal-app/src/app/features/kb/kb-list.component.html frontend/projects/common/src/lib/testing/rtl-icon.spec.ts
git commit -m "feat(rtl): logical properties + semantic icon mirroring (US-312 T2)"
```

## Definition of done

`AC-312.1` (containers/spacing/forms/tables mirror) covered by the zero-offender `rtl-safety.spec.ts`
sweep across `.html` and inline `.ts`. `AC-312.2` (directional cues mirror only when semantic) covered
by `rtl-icon.spec.ts` + the `rtl-mirror` rule; non-directional icons deliberately left un-mirrored.
Full gate:

```powershell
cd frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test --grep "RTL|Arabic|rtl|arabic"
```
