# Admin UI Redesign — Implementation Plan

> **Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.**

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace inline "create" forms with modal dialogs (`cs-dialog`), add a sidebar collapse
toggle, and move change-password into a new Profile page reached from the sidebar's identity footer.
**Shipped already** — below is the real code (verified: `cs-dialog` exists at
`common/src/lib/ui/dialog.component.ts`, `profile.component.ts` at
`admin-app/src/app/features/account/profile.component.ts`).

**Architecture:** `frontend/` only. No backend change.

**Tech Stack:** Angular 20 standalone + signals, Tailwind v4.

**Explicit deviation from this project's normal TDD convention:** per direct instruction ("skip
testing for saving tokens"), this pass wrote no test files and ran no test suites. Every task below is
implementation-only and was **not** visually verified in a browser. Recorded plainly, not hidden.

**Shipped already — retroactive code-bearing plan.** Disclosure line above records that.

---

### Task 1: `CsDialog` (`common/ui`)

**Files:**
- Read: `frontend/projects/common/src/lib/ui/dialog.component.ts`
- Read: `frontend/projects/common/src/lib/ui/dialog.component.html`
- Read: `frontend/projects/common/src/public-api.ts` (export)
- Read: `frontend/projects/common/src/lib/i18n/translations.ts` (`dialog.close`)

**Interfaces:** Produces `cs-dialog` (selector), `open: boolean` (required input), `heading?: string`
input, `closed: void` output.

- [ ] **Step 1: Confirm the shipped component**

```typescript
// common/src/lib/ui/dialog.component.ts
import { ChangeDetectionStrategy, Component, ElementRef, effect, input, output, viewChild } from '@angular/core';
import { CsIcon } from './icon.component';
import { TranslatePipe } from '../i18n/translate.pipe';

@Component({
  selector: 'cs-dialog',
  imports: [CsIcon, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dialog.component.html',
})
export class CsDialog {
  readonly open = input.required<boolean>();
  readonly heading = input<string>();
  readonly closed = output<void>();
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  constructor() {
    effect(() => {
      if (this.open()) {
        queueMicrotask(() => this.panel()?.nativeElement.focus());
      }
    });
  }
  dismiss(): void { this.closed.emit(); }
  onBackdropKeydown(event: KeyboardEvent): void { if (event.key === 'Escape') this.dismiss(); }
}
```

```html
<!-- dialog.component.html -->
@if (open()) {
  <div class="fixed inset-0 z-50 grid place-items-center bg-on-surface/40 p-4"
       (click)="dismiss()" (keydown)="onBackdropKeydown($event)">
    <div #panel role="dialog" aria-modal="true" [attr.aria-label]="heading()"
         tabindex="-1" (click)="$event.stopPropagation()"
         class="flex max-h-[90vh] w-full max-w-lg flex-col overflow-hidden rounded-xl border border-border-subtle bg-surface-lowest shadow-popover outline-none">
      @if (heading(); as title) {
        <div class="flex items-center justify-between border-b border-border-subtle px-6 py-4">
          <h2 class="font-display text-headline-md text-on-surface">{{ title }}</h2>
          <button type="button" (click)="dismiss()" [attr.aria-label]="'dialog.close' | t"
                  class="grid size-8 shrink-0 place-items-center rounded text-on-surface-variant hover:bg-surface-high">
            <cs-icon name="close" [size]="20" />
          </button>
        </div>
      }
      <div class="overflow-y-auto p-6"><ng-content /></div>
    </div>
  </div>
}
```

- [ ] **Step 2: Run the (skipped) test**

Run: `cd frontend && npx ng test common --watch=false --filter="dialog"`
Expected: **Not run this pass** — no spec written (deviation above). Would assert `role="dialog"`
presence and `closed` emits on backdrop click + Escape.

- [ ] **Step 3: Commit** — not committed this session (staged only, per standing instruction).

---

### Task 2: Departments create form → `CsDialog`

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/organisation/departments.component.ts`/`.html`

**Interfaces:** Component gains `showCreate = signal(false)`; the create success handler closes the
dialog.

- [ ] **Step 1: Confirm the shipped change**

```typescript
// departments.component.ts — added
readonly showCreate = signal(false);
```

```typescript
// departments.component.ts — create() success handler
this.api.create({ name }).subscribe({
  next: () => { this.saving.set(false); this.form.reset(); this.showCreate.set(false); this.load(); },
  // …
});
```

```html
<!-- departments.component.html — header trigger + dialog -->
<header class="flex flex-wrap items-center justify-between gap-4">
  <h1 class="font-display text-headline-lg">{{ 'departments.title' | t }}</h1>
  <cs-button (pressed)="showCreate.set(true)"><cs-icon name="add" />{{ 'departments.add' | t }}</cs-button>
</header>
<cs-dialog [open]="showCreate()" [heading]="'departments.create.title' | t" (closed)="showCreate.set(false)">
  <form [formGroup]="form" (ngSubmit)="create()" class="flex flex-col gap-4">
    <!-- same fields as before, no longer always-visible -->
  </form>
</cs-dialog>
```

- [ ] **Step 2: Run the (skipped) test**

Run: `cd frontend && npx ng test admin-app --watch=false --filter="departments"`
Expected: **Not run this pass.**

- [ ] **Step 3: Commit** — not committed this session.

---

### Task 3: SLA Policies + Users create forms → `CsDialog`

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/organisation/sla-policies.component.ts`/`.html`
- Modify: `frontend/projects/admin-app/src/app/features/users/users.component.ts`/`.html`

**Interfaces:** SLA Policies reuses its pre-existing `editingId` signal for a second edit dialog
(`editingId() !== null`); the row now shows only `Edit`/`Deactivate` buttons. Users follows the
departments pattern exactly.

- [ ] **Step 1: Confirm shipped shape**

```html
<!-- sla-policies.component.html — edit dialog reusing editingId() -->
<cs-dialog [open]="editingId() !== null" [heading]="'sla.edit.title' | t" (closed)="cancelEdit()">
  <form [formGroup]="editForm" (ngSubmit)="saveEdit()">…</form>
</cs-dialog>
```

- [ ] **Step 2: Run (skipped)**

Run: `cd frontend && npx ng test admin-app --watch=false`
Expected: **Not run this pass.**

- [ ] **Step 3: Commit** — not committed this session.

---

### Task 4: Sidebar collapse

**Files:**
- Modify: `frontend/projects/admin-app/src/app/layout/shell.component.ts`/`.html`
- Modify: `frontend/projects/common/src/styles/theme.css` (`--spacing-sidebar-collapsed: 4.5rem`)
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts` (`sidebar.collapse`/`sidebar.expand`)

**Interfaces:** `AdminShell.collapsed: Signal<boolean>`, `toggleCollapsed(): void`, persisted to
`localStorage`.

- [ ] **Step 1: Confirm the shipped code**

```typescript
// shell.component.ts
protected readonly collapsed = signal(this.readStoredCollapsed());
toggleCollapsed(): void {
  const next = !this.collapsed();
  this.collapsed.set(next);
  try { localStorage.setItem('admin-shell:sidebar-collapsed', String(next)); } catch { /* private mode */ }
}
private readStoredCollapsed(): boolean {
  try { return localStorage.getItem('admin-shell:sidebar-collapsed') === 'true'; } catch { return false; }
}
```

```html
<!-- shell.component.html — nav width toggle -->
<nav class="relative … transition-[width] duration-200"
     [class.w-sidebar]="!collapsed()" [class.w-sidebar-collapsed]="collapsed()">
  <button type="button" (click)="toggleCollapsed()"
          [attr.aria-label]="(collapsed() ? 'sidebar.expand' : 'sidebar.collapse') | t" …>
    <cs-icon [name]="collapsed() ? 'chevron_right' : 'chevron_left'" [size]="16" />
  </button>
  <!-- app name, nav labels, identity footer name each gain @if (!collapsed()) { … } -->
</nav>
```

`--spacing-sidebar-collapsed: 4.5rem` added next to `--spacing-sidebar: 17.5rem`; Tailwind v4
auto-generates `w-sidebar-collapsed` from the `--spacing-*` token.

- [ ] **Step 2: Run (skipped)**

Run: `cd frontend && npx ng test admin-app --watch=false --filter="shell"`
Expected: **Not run this pass.**

- [ ] **Step 3: Commit** — not committed this session.

---

### Task 5: Profile page replacing `/account/password`

**Files:**
- Create: `frontend/projects/admin-app/src/app/features/account/profile.component.ts`/`.html`
- Delete: `frontend/projects/admin-app/src/app/features/account/change-password.component.ts`/`.html`/`.spec.ts`
- Modify: `frontend/projects/admin-app/src/app/app.routes.ts`
- Modify: `frontend/projects/admin-app/src/app/layout/shell.component.ts` (NavItem gains `hidden?`;
  `nav` computed filters hidden; identity footer becomes `<a routerLink="/profile">`)
- Modify: `frontend/projects/admin-app/src/app/layout/shell.component.spec.ts` (route title `Password`→`Profile`)

**Interfaces:** `ProfileComponent` reuses `StaffApi.changeOwnPassword` and `SessionStore.displayName`/
`roles`.

- [ ] **Step 1: Confirm the shipped component**

```typescript
// profile.component.ts (excerpt)
export default class ProfileComponent {
  private readonly api = inject(StaffApi);
  private readonly session = inject(SessionStore);
  protected readonly displayName = this.session.displayName;
  protected readonly roles = this.session.roles;
  readonly busy = signal(false);
  readonly error = signal<ApiError | null>(null);
  readonly done = signal(false);
  readonly form = new FormGroup({
    currentPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    newPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(12)] }),
  });
  submit(): void {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true); this.error.set(null); this.done.set(false);
    const { currentPassword, newPassword } = this.form.getRawValue();
    this.api.changeOwnPassword(currentPassword, newPassword).subscribe({
      next: () => { this.busy.set(false); this.done.set(true); this.form.reset(); }, // AUTH-17
      error: (failure: unknown) => { /* map to ApiError */ },
    });
  }
}
```

```typescript
// app.routes.ts
{ path: 'profile', loadComponent: () => import('./features/account/profile.component') }
```

```typescript
// shell.component.ts — NavItem gains hidden
export interface NavItem { readonly path: string; readonly key: TranslationKey; readonly icon: string;
  readonly adminOnly?: true; readonly supervisorOrAdmin?: true; readonly hidden?: true; }
// NAV_ITEMS: { path: '/profile', key: 'nav.profile', icon: 'person', hidden: true }
// nav() computed filters !item.hidden && role checks
```

- [ ] **Step 2: Run (skipped except the one edited spec)**

Run: `cd frontend && npx ng test admin-app --watch=false --filter="shell"`
Expected: **shell.component.spec.ts** was edited to keep `Profile` title accurate but was **not
re-run** this pass; other component specs were not added.

- [ ] **Step 3: Commit** — not committed this session.

---

### Task 6: Build gate (definition of done, not met this pass)

- [ ] **Step 1: Build**

Run: `cd frontend && npx ng build admin-app`
Expected: **Not run this pass.** Both `ng build admin-app` and a manual click-through of Departments,
SLA Policies, Users, the sidebar toggle and `/profile` are required before this is trusted.

- [ ] **Step 2: Commit** — not committed this session.

## Definition of done (not met this pass)

Every task is implementation-only: no test written, no test run, no `ng build`/browser verification.
Before trusting: `npx ng build admin-app` green and a manual click-through of each affected surface.
