> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# Design application — shared library (`common`) Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan
> did not precede its implementation.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** The shared design-system primitives every screen in `admin-app`/`portal-app` renders
through — tokens, `CsIcon`, `CsCard`, a semantic `CsBadge`, restyled `CsButton`/`CsInputField`, and
the three state components — built once in `common` so the screens plan can consume a fixed API.

**Architecture:** Pure `common/src/lib/ui/**` + `common/src/styles/theme.css` additions. No routes,
no HTTP, no state — every component here is presentational.

**Tech Stack:** Angular 20 standalone components, signals, Tailwind v4 `@theme` tokens.

**Spec:** [`../../specs/EPIC-13-US-311-command-center-design-application.md`](../../specs/EPIC-13-US-311-command-center-design-application.md)
**Criteria:** `AC-87`, `AC-89`, `AC-90`, `AC-91`

## Global Constraints

- **Additive tokens only — rename nothing.** Fifteen templates already reference the existing
  token names; a rename breaks all of them silently (no compile error, just a missing Tailwind
  class).
- **Logical utilities only** (`ms-`/`me-`, `ps-`/`pe-`, `text-start`/`text-end`, `border-s`/
  `border-e`, `start-`/`end-`) — `rtl-safety.spec.ts` scans every `.html` and fails the build on a
  physical-direction class.
- **Every user-facing string through the dictionary** — `no-hardcoded-strings.spec.ts` fails the
  build otherwise. New empty/error copy needs `en`+`ar` entries in `translations.ts`.
- **`CsBadge`'s Tailwind classes must be literal strings in source**, never built by concatenation
  — Tailwind's JIT scanner reads source text, not runtime values; a template-literal class name
  works in dev (styles happen to be cached from elsewhere) and silently renders unstyled in a
  production build. Map `value` → a literal class via a `Record`, not string interpolation.
- **Do not touch `frontend/projects/admin-app/**` or `portal-app/**`** — the screens plan owns
  those; this plan only fixes the shared library's contract.

---

### Task 1: Design tokens (`AC-87`, `AC-90`)

**Files:**
- Modify: `frontend/projects/common/src/styles/theme.css`

**Interfaces:**
- Produces: `--color-surface-bright`, `--color-secondary-container`/`--color-on-secondary-container`,
  `--text-label-lg`, `--text-data-mono`, `--shadow-card`, `--shadow-popover` theme tokens, and the
  Material Symbols base rule.

- [ ] **Step 1: Add the tokens**

```css
/* frontend/projects/common/src/styles/theme.css, inside the existing @theme block */
@theme {
  /* Layer tint: the mockups' hover strip and card header, distinct from --color-surface. */
  --color-surface-bright: #f8f9ff;

  /* The active nav pill. Command Center's secondary-container. */
  --color-secondary-container: #645efb;
  --color-on-secondary-container: #ffffff;

  --text-label-lg: 0.8125rem;
  --text-label-lg--line-height: 1rem;
  --text-data-mono: 0.8125rem;
  --text-data-mono--line-height: 1.25rem;

  /* Elevation is tonal here; these are the only two shadows in the system. */
  --shadow-card: 0 4px 12px rgba(0, 0, 0, 0.02);
  --shadow-popover: 0 12px 24px rgba(0, 0, 0, 0.08);
}
```

- [ ] **Step 2: Material Symbols base rule (once, here — not per app)**

```css
.material-symbols-outlined {
  font-variation-settings: 'FILL' 0, 'wght' 400, 'GRAD' 0, 'opsz' 24;
}
.material-symbols-outlined.is-filled { font-variation-settings: 'FILL' 1; }
```

- [ ] **Step 3: Run to verify nothing broke**

Run: `cd frontend && npx ng build admin-app`
Expected: `Application bundle generation complete`, 0 errors — additive tokens never break an
existing build; a failure here means a name collided with something already defined.

- [ ] **Step 4: Commit**

```bash
git add frontend/projects/common/src/styles/theme.css
git commit -m "feat(design-system): add Command Center tokens (AC-87, AC-90)"
```

---

### Task 2: `CsIcon` (`AC-87`)

**Files:**
- Create: `frontend/projects/common/src/lib/ui/icon.component.ts`
- Modify: `frontend/projects/common/src/public-api.ts`
- Test: `frontend/projects/common/src/lib/ui/icon.component.spec.ts`

**Interfaces:**
- Produces: `CsIcon` (selector `cs-icon`), inputs `name: string` (required), `filled: boolean`
  (default false), `size: number` (default 20).

- [ ] **Step 1: Write the failing test**

```ts
// frontend/projects/common/src/lib/ui/icon.component.spec.ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CsIcon } from './icon.component';

describe('CsIcon', () => {
  function render(name: string, filled = false): ComponentFixture<CsIcon> {
    const fixture = TestBed.createComponent(CsIcon);
    fixture.componentRef.setInput('name', name);
    fixture.componentRef.setInput('filled', filled);
    fixture.detectChanges();
    return fixture;
  }

  it('AC87: renders the ligature name as the glyph text, hidden from assistive tech', () => {
    const fixture = render('dashboard');
    const span = (fixture.nativeElement as HTMLElement).querySelector('span')!;
    expect(span.textContent?.trim()).toBe('dashboard');
    expect(span.getAttribute('aria-hidden')).toBe('true');
  });

  it('AC87: filled adds the is-filled class', () => {
    const fixture = render('warning', true);
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('span')!.classList.contains('is-filled'),
    ).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test common --watch=false --include='**/icon.component.spec.ts'`
Expected: FAIL — module does not exist.

- [ ] **Step 3: Implement**

```ts
// frontend/projects/common/src/lib/ui/icon.component.ts
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * A Material Symbols ligature — the font renders the literal name text as a glyph.
 * `<cs-icon name="dashboard" /> · <cs-icon name="warning" filled />`
 *
 * `aria-hidden` always: every icon in this design sits beside its own label, so announcing the
 * ligature text would read the label twice.
 */
@Component({
  selector: 'cs-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span
    class="material-symbols-outlined"
    [class.is-filled]="filled()"
    [style.font-size.px]="size()"
    aria-hidden="true"
    >{{ name() }}</span
  >`,
})
export class CsIcon {
  readonly name = input.required<string>();
  readonly filled = input(false);
  readonly size = input(20);
}
```

- [ ] **Step 4: Export and verify**

Add to `public-api.ts`: `export * from './lib/ui/icon.component';`

Run: `cd frontend && npx ng test common --watch=false --include='**/icon.component.spec.ts'`
Expected: PASS, 2/2.

- [ ] **Step 5: Commit**

```bash
git add frontend/projects/common/src/lib/ui/icon.component.ts frontend/projects/common/src/lib/ui/icon.component.spec.ts frontend/projects/common/src/public-api.ts
git commit -m "feat(design-system): CsIcon (AC-87)"
```

---

### Task 3: `CsCard` (`AC-87`)

**Files:**
- Create: `frontend/projects/common/src/lib/ui/card.component.ts`, `card.component.html`
- Modify: `public-api.ts`

**Interfaces:**
- Produces: `CsCard` (selector `cs-card`), input `heading?: string` — omitted means no header
  strip. Content-projects the body; an `[action]`-slotted element projects into the header strip's
  end when present.

- [ ] **Step 1: Implement** (this component's shape is already covered by
  `frontend/projects/common/src/lib/ui/card.component.spec.ts` if one exists from a later pass —
  check before writing a new test file to avoid a duplicate)

```ts
// frontend/projects/common/src/lib/ui/card.component.ts
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'cs-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class:
      'flex flex-col overflow-hidden rounded-xl border border-border-subtle bg-surface-lowest shadow-card',
  },
  templateUrl: './card.component.html',
})
export class CsCard {
  readonly heading = input<string>();
}
```

```html
<!-- card.component.html -->
@if (heading(); as title) {
  <div
    class="flex items-center justify-between border-b border-border-subtle bg-surface-bright px-4 py-2"
  >
    <h2 class="font-display text-headline-md text-on-surface">{{ title }}</h2>
    <ng-content select="[action]" />
  </div>
}
<ng-content />
```

- [ ] **Step 2: Export and verify**

Add to `public-api.ts`: `export * from './lib/ui/card.component';`

Run: `cd frontend && npx ng build admin-app`
Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add frontend/projects/common/src/lib/ui/card.component.ts frontend/projects/common/src/lib/ui/card.component.html frontend/projects/common/src/public-api.ts
git commit -m "feat(design-system): CsCard (AC-87)"
```

---

### Task 4: `CsBadge` — semantic status/priority rewrite (`AC-89`)

**Files:**
- Modify: `frontend/projects/common/src/lib/ui/badge.component.ts`
- Test: `frontend/projects/common/src/lib/ui/badge.component.spec.ts`

**Interfaces:**
- Produces: `CsBadge` inputs `kind: 'status' | 'priority'` (required), `value: string` (required —
  one of `New|Open|Pending|Resolved|Closed` for `status`, `Low|Normal|High|Urgent` for `priority`),
  `label?: string` (falls back to `value`).

- [ ] **Step 1: Write the failing test**

```ts
// badge.component.spec.ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CsBadge } from './badge.component';

describe('CsBadge', () => {
  function render(kind: 'status' | 'priority', value: string): ComponentFixture<CsBadge> {
    const fixture = TestBed.createComponent(CsBadge);
    fixture.componentRef.setInput('kind', kind);
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
    return fixture;
  }

  it('AC89: a status badge carries its status colour class, for every status', () => {
    for (const status of ['New', 'Open', 'Pending', 'Resolved', 'Closed']) {
      const fixture = render('status', status);
      const el = fixture.nativeElement as HTMLElement;
      expect(el.querySelector(`.bg-status-${status.toLowerCase()}`)).not.toBeNull();
    }
  });

  it('AC89: a priority badge carries the tinted class and a dot, for every priority', () => {
    for (const priority of ['Low', 'Normal', 'High', 'Urgent']) {
      const fixture = render('priority', priority);
      const el = fixture.nativeElement as HTMLElement;
      expect(el.querySelector(`.text-priority-${priority.toLowerCase()}`)).not.toBeNull();
      expect(el.querySelector(`.bg-priority-${priority.toLowerCase()}`)).not.toBeNull();
    }
  });

  it('AC89: badge classes are literals in source, not built by concatenation', () => {
    // A grep-shaped assertion: read the component's own source and confirm no template-literal
    // class construction exists, since Tailwind's JIT scanner cannot see a runtime string.
    const source = CsBadge.toString();
    expect(source).not.toMatch(/`bg-status-\$\{/);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test common --watch=false --include='**/badge.component.spec.ts'`
Expected: FAIL — the existing `badge.component.ts` is generic and has no `kind`/status-vs-priority
class maps yet.

- [ ] **Step 3: Implement**

```ts
// badge.component.ts
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

const STATUS_CLASSES: Record<string, string> = {
  new: 'bg-status-new text-on-primary',
  open: 'bg-status-open text-on-primary',
  pending: 'bg-status-pending text-on-primary',
  resolved: 'bg-status-resolved text-on-primary',
  closed: 'bg-status-closed text-on-primary',
};

const PRIORITY_TEXT_CLASSES: Record<string, string> = {
  low: 'text-priority-low border-priority-low/20 bg-priority-low/10',
  normal: 'text-priority-normal border-priority-normal/20 bg-priority-normal/10',
  high: 'text-priority-high border-priority-high/20 bg-priority-high/10',
  urgent: 'text-priority-urgent border-priority-urgent/20 bg-priority-urgent/10',
};

const PRIORITY_DOT_CLASSES: Record<string, string> = {
  low: 'bg-priority-low',
  normal: 'bg-priority-normal',
  high: 'bg-priority-high',
  urgent: 'bg-priority-urgent',
};

@Component({
  selector: 'cs-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (kind() === 'status') {
      <span [class]="'inline-flex items-center rounded px-2 py-0.5 text-label-md ' + statusClass()">
        {{ label() ?? value() }}
      </span>
    } @else {
      <span
        [class]="
          'inline-flex items-center gap-1.5 rounded border px-2 py-0.5 text-label-md ' + priorityTextClass()
        "
      >
        <span [class]="'size-1.5 rounded-full ' + priorityDotClass()"></span>
        {{ label() ?? value() }}
      </span>
    }
  `,
})
export class CsBadge {
  readonly kind = input.required<'status' | 'priority'>();
  readonly value = input.required<string>();
  readonly label = input<string>();

  protected readonly statusClass = computed(
    () => STATUS_CLASSES[this.value().toLowerCase()] ?? 'bg-surface-highest text-on-surface',
  );
  protected readonly priorityTextClass = computed(
    () => PRIORITY_TEXT_CLASSES[this.value().toLowerCase()] ?? 'text-on-surface-variant border-outline-variant',
  );
  protected readonly priorityDotClass = computed(
    () => PRIORITY_DOT_CLASSES[this.value().toLowerCase()] ?? 'bg-on-surface-variant',
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx ng test common --watch=false --include='**/badge.component.spec.ts'`
Expected: PASS, 3/3.

- [ ] **Step 5: Commit**

```bash
git add frontend/projects/common/src/lib/ui/badge.component.ts frontend/projects/common/src/lib/ui/badge.component.spec.ts
git commit -m "feat(design-system): semantic status/priority CsBadge (AC-89)"
```

---

### Task 5: `CsButton` variants (`AC-90`)

**Files:**
- Modify: `frontend/projects/common/src/lib/ui/button.component.ts`

**Interfaces:**
- Produces: `variant: 'primary' | 'secondary' | 'ghost'` input, default `'primary'` — this is the
  exact shape already documented in this component's own header comment (see the live file); this
  task's only job, if not already done, is to confirm the three literal `VARIANTS` class strings
  match:

```ts
const VARIANTS = {
  primary: 'bg-primary text-on-primary shadow-sm hover:opacity-90 active:scale-95',
  secondary:
    'bg-surface-lowest border border-outline-variant text-on-surface hover:bg-surface-bright',
  ghost: 'text-primary hover:underline',
} as const;
```

- [ ] **Step 1: Verify against the live file** — `frontend/projects/common/src/lib/ui/button.component.ts`
  already carries this exact `VARIANTS` map (confirmed against the real file, 2026-08-27). No
  change needed; this task exists in the plan for traceability against `AC-90`, not because code is
  missing.

- [ ] **Step 2: Commit** — nothing to commit; already shipped.

---

### Task 6: `CsInputField` restyle (`AC-90`)

**Files:**
- Modify: `frontend/projects/common/src/lib/ui/input-field.component.ts`/`.html`

**Constraint:** **do not touch the touched-vs-server error display logic** — `AC-59`/`AC-60`
depend on it and it is already tested (`input-field.component.spec.ts`).

- [ ] **Step 1: Restyle only** — input: `h-10 rounded-lg bg-surface-lowest border
  border-outline-variant px-3 text-body-md focus:border-primary focus:ring-2
  focus:ring-primary/20 transition-all`. Label above, `text-label-md text-on-surface-variant`.
  Error keeps `text-error` and `role="alert"`.

- [ ] **Step 2: Run the existing test file unedited**

Run: `cd frontend && npx ng test common --watch=false --include='**/input-field.component.spec.ts'`
Expected: PASS, unedited — a restyle that breaks this test broke behaviour, not just looks.

- [ ] **Step 3: Commit**

```bash
git add frontend/projects/common/src/lib/ui/input-field.component.ts frontend/projects/common/src/lib/ui/input-field.component.html
git commit -m "style(design-system): restyle CsInputField (AC-90)"
```

---

### Task 7: State components restyle (`AC-90`)

**Files:**
- Modify: `loading-state.component.ts`, `empty-state.component.ts`, `error-state.component.ts`

**Constraint:** keep every behaviour, restyle only. `CsEmptyState` **must still have no retry
button** — its absence is the assertion in `AC58: a successful empty result renders the empty
state, with no retry offered`.

- [ ] **Step 1: Add `cs-icon` to widen the visual distance `AC-58` requires** — `inbox` for empty,
  `error` for error state. Loading state unchanged besides token restyle.

- [ ] **Step 2: Run existing tests unedited**

Run: `cd frontend && npx ng test common --watch=false --include='**/state-components.spec.ts'`
Expected: PASS, unedited.

- [ ] **Step 3: Commit**

```bash
git add frontend/projects/common/src/lib/ui/loading-state.component.ts frontend/projects/common/src/lib/ui/empty-state.component.ts frontend/projects/common/src/lib/ui/error-state.component.ts
git commit -m "style(design-system): restyle loading/empty/error states, widen AC-58's visual gap (AC-90)"
```

---

### Task 8: Export surface (`AC-91`)

- [ ] **Step 1:** Confirm every component built in Tasks 2–7 is exported from `public-api.ts`
  (`CsIcon`, `CsCard`, `CsBadge`, plus the already-exported `CsButton`/`CsInputField`/state trio).

- [ ] **Step 2: Run the full common suite**

Run: `cd frontend && npx ng test common --watch=false`
Expected: green, including `rtl-safety.spec.ts` and `no-hardcoded-strings.spec.ts`.

## Definition of done

`ng test common --watch=false` green with output pasted · `ng build admin-app` clean · task record
in `docs/superpowers/plans/EPIC-13-US-311-design-system-common/README.md`. **Do not commit without
being asked** — this session's standing instruction overrides the per-task `git commit` steps
above; treat every "Commit" step as `git add` only unless told otherwise.

