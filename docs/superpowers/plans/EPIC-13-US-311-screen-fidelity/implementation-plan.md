# Screen fidelity — Implementation Plan

> **Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.**

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Match the Command Center design screens against their mockups across already-shipped
admin-app and common screens (`AC-93`…`AC-100`). Behaviour is frozen — only templates, tokens and
i18n change. **Shipped already**; this is the code-bearing record (verified against the tree, e.g.
`cs-placeholder` exists at `common/src/lib/ui/placeholder.component.ts`).

**Architecture:** `frontend/projects/common` (design system) + `frontend/projects/admin-app`
(screens). Composes `cs-card`, `cs-badge`, `cs-icon`, `cs-placeholder` — does not rebuild them.

**Tech Stack:** Angular 20 standalone components, signals, Tailwind v4 tokens. No new packages.

**Spec:** [`../../specs/EPIC-13-US-311-screen-fidelity-design.md`](../../specs/EPIC-13-US-311-screen-fidelity-design.md)

**Shipped already — retroactive code-bearing plan.** Disclosure line above records that.

## Global Constraints

- **Behaviour frozen** (`AC-100`): no signal, HTTP call, route or state transition changes. Every
  pre-existing test passes unedited; a failure means the restyle broke something — fix the template.
- **Logical utilities only**: every mockup class passes through the prior spec's translation table.
  `rtl-safety.spec.ts` scans every `.html` including comments — never name a physical utility in a
  comment.
- **Every user-facing string through `| t`**, both languages (`no-hardcoded-strings.spec.ts`).
- **No invented data** (`AC-97`): every unbacked field goes through `cs-placeholder`.
- **No new control for a capability that does not exist** (`AC-92`).

---

### Task 1: `cs-placeholder` (`AC-97`, `AC-92`)

**Files:**
- Read: `frontend/projects/common/src/lib/ui/placeholder.component.ts`
- Read: `frontend/projects/common/src/lib/ui/placeholder.component.html`
- Read: `frontend/projects/common/src/lib/i18n/translations.ts` (`field.notRecorded`)
- Read: `frontend/projects/common/src/public-api.ts` (export)

**Interfaces:** Produces `cs-placeholder` (selector), input `field: string` (required, the dictionary
key of the absent field). Deliberately **not** a control.

- [ ] **Step 1: Confirm the shipped component**

```typescript
// common/src/lib/ui/placeholder.component.ts
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '../i18n/translate.pipe';

@Component({
  selector: 'cs-placeholder',
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './placeholder.component.html',
})
export class CsPlaceholder {
  /** The dictionary key of the field standing empty — `customers.profile.mrr`. */
  readonly field = input.required<string>();
}
```

```html
<!-- placeholder.component.html — renders the shared "not recorded" string, not a control -->
<dd class="italic text-on-surface-variant/60" [attr.data-field]="field()">
  {{ 'field.notRecorded' | t }}
</dd>
```

- [ ] **Step 2: Unit test asserts it is not a control**

```typescript
// placeholder.component.spec.ts
it('AC-92: is a read-only label, never a button/a/input', () => {
  const fixture = TestBed.createComponent(CsPlaceholder);
  fixture.componentInstance.field.set('customers.profile.mrr');
  fixture.detectChanges();
  const el = fixture.nativeElement.querySelector('dd');
  expect(el.tagName).not.toBe('BUTTON');
  expect(el.tagName).not.toBe('A');
  expect(el.querySelector('input,button,a')).toBeNull();
});
```

- [ ] **Step 3: Run the test**

Run: `cd frontend && npx ng test common --watch=false --filter="placeholder"`
Expected: PASS.

- [ ] **Step 4: Commit** — already committed when shipped.

---

### Task 2: Activity feed model — dropped (`AC-92`, `G-7`)

**Files:** (no new file) — decision recorded, not code.

**Interfaces:** n/a. Verified against `common/src/lib/tickets/ticket.api.ts` that there is **no**
`customerId` filter on the queue endpoint, so "this customer's tickets" cannot be answered by the
queue; attachments load via the end rail's own component (re-fetching would create two sources of
truth). With notes as the only populated lane, a merge function is dead code. The ticket lane renders
an explicit *not available on this screen yet* line in its designed position.

- [ ] **Step 1: Record the gap**

No code. The fix (a `customerId` filter on the queue endpoint) is a backend change with its own gate,
not smuggled into a restyle. Recorded in the spec as `G-7`.

- [ ] **Step 2: Commit** — n/a.

---

### Task 3: Identity band on the customer profile (`AC-93`, `AC-97`)

**Files:**
- Read: `frontend/projects/admin-app/src/app/features/customers/customer-detail.component.html`
- Read: `frontend/projects/admin-app/src/app/features/customers/customer-detail.component.ts`

**Interfaces:** Reuses existing `startEdit()` / `askToDelete()` methods and the `data-testid` hooks.
The `verified` glyph and `New Ticket`/`Edit`/`Remove` actions already exist; the redesign composes
them into a full-bleed band.

- [ ] **Step 1: Confirm the band markup**

```html
<!-- customer-detail.component.html — identity band (shipped) -->
<header class="relative flex flex-wrap items-center justify-between gap-4">
  <div class="flex items-center gap-4">
    <div class="grid size-20 place-items-center rounded-full bg-surface-high font-display text-headline-md">
      {{ initial }}
    </div>
    <div>
      <h1 class="font-display text-headline-lg text-on-surface">{{ customer().fullName }}</h1>
      <cs-placeholder field="customers.profile.jobTitle" /> <!-- unbacked -->
      <cs-badge>…</cs-badge>
    </div>
  </div>
  <div class="flex items-center gap-2">
    <cs-button (pressed)="router.navigate(['/tickets/new'])">…</cs-button>
    <cs-button (pressed)="startEdit()">…</cs-button>
    <cs-button (pressed)="askToDelete()">…</cs-button>
  </div>
</header>
```

- [ ] **Step 2: Build to prove `data-testid`s preserved**

Run: `cd frontend && npx ng test admin-app --watch=false --filter="customer-detail"`
Expected: PASS — pre-existing tests green, unedited.

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 4: 3 / 6 / 3 workspace (`AC-93`…`AC-96`)

**Files:**
- Read: `frontend/projects/admin-app/src/app/features/customers/customer-detail.component.ts`/`.html`
- Read: `frontend/projects/admin-app/src/app/features/customers/customer-notes.component.ts`
- Read: `frontend/projects/admin-app/src/app/features/customers/customer-attachments.component.ts`

**Interfaces:** `customer-notes` and `customer-attachments` keep their current API; only templates
change. Start rail = Contact Info + Account Details (real `id`, `createdAt`; placeholders for MRR /
timezone / manager). Centre = edit form when editing, then notes timeline, then the ticket lane's
*not available* line. End rail = Files & Attachments, restyled.

- [ ] **Step 1: Confirm the column grid**

```html
<div class="grid grid-cols-1 gap-6 lg:grid-cols-12">
  <section class="lg:col-span-3">… Contact Info / Account Details …</section>
  <section class="lg:col-span-6">… notes / edit form …</section>
  <section class="lg:col-span-3">… Files & Attachments …</section>
</div>
```

- [ ] **Step 2: Run rtl-safety + no-hardcoded-strings**

Run: `cd frontend && npx ng test common --watch=false && npx ng test admin-app --watch=false`
Expected: green — both guard specs included.

- [ ] **Step 3: Commit** — already committed when shipped.

---

### Task 5: Checkpoint (`AC-98`)

- [ ] **Step 1: Build + test the customer screen**

Run: `cd frontend && npx ng test common --watch=false && npx ng test admin-app --watch=false && npx ng build admin-app`
Expected: green + clean build. Paste output.

- [ ] **Step 2: Commit** — already committed when shipped.

---

### Task 6–T10: Remaining screens (`AC-98`)

**Files (each follows the same restyle-only rule):**
- `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.*` — segmented filter bar.
- `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.*` — two-column workspace.
- `frontend/projects/admin-app/src/app/features/tickets/ticket-new.component.*` — centred single-column form.
- `frontend/projects/admin-app/src/app/features/dashboard/dashboard.component.*` — tile-row proportions.
- `frontend/projects/admin-app/src/app/features/customers/customer-list.component.*` and `features/users/users.component.*`.

**Interfaces:** behaviour frozen — `data-testid`s and route guards unchanged.

- [ ] **Step 1: Apply the mockup's template composition per screen**

Each screen's existing template is rearranged to the mockup's structure (e.g. the queue's twelve-column
grid already matches; it gains the mockup's row composition). No signal/HTTP/route change.

- [ ] **Step 2: Gate**

Run: `cd frontend && npx ng test admin-app --watch=false && npx ng build admin-app`
Expected: green + clean. Paste output.

- [ ] **Step 3: Commit** — already committed when shipped.

## Definition of done

`ng test common` and `ng test admin-app` green (unedited pre-existing tests), `ng build admin-app`
clean, `rtl-safety`/`no-hardcoded-strings` green, `cs-placeholder` present and proven non-control.
An explicit list of rebuilt-vs-not screens recorded in `tasks/`.
