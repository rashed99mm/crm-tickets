# Task 06 — Dialog: Escape, focus, details list (US-807, AC-807.2, AC-807.3, AC-807.4)

**Files:**
- Modify: `frontend/projects/common/src/lib/ui/confirmation-host.component.ts` (14 lines today)
- Modify: `frontend/projects/common/src/lib/ui/confirmation-host.component.html` (whole file)
- Test: `frontend/projects/common/src/lib/ui/confirmation-host.component.spec.ts` (**create** — no spec exists)

**Interfaces:**
- Consumes: `ConfirmationService.current()` / `.resolve()` and the new `details` field (Task 05);
  the focus-on-open pattern already proven in `CsDialog` — `effect` + `queueMicrotask` +
  `viewChild<ElementRef<HTMLElement>>` (`dialog.component.ts:30-40`); the Escape-on-backdrop pattern
  at `dialog.component.ts:46-50` with `(keydown)` bound on the backdrop
  (`dialog.component.html:5`).
- Produces: nothing consumed by later tasks beyond the rendered `details` list, which
  `AC-806.12`'s test in Task 09 asserts through this host.

**What is broken.** `confirmation-host.component.html` renders `role="alertdialog"` with
`aria-modal="true"`, a backdrop that closes on click, and two buttons — but there is no Escape
handler, nothing receives focus when it opens, and focus is not returned when it closes. A keyboard
user gets a modal they cannot dismiss and cannot reach, with their place on the page lost afterwards.

**Why focus goes to Cancel for a `danger` request.** The dialog exists to slow down a destructive
action. Landing focus on the confirm button turns "press Enter to get past the modal" into the
destructive act itself.

## Steps

- [ ] **Step 1: Write the failing tests**

Create `frontend/projects/common/src/lib/ui/confirmation-host.component.spec.ts`:

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfirmationService } from './confirmation.service';
import { CsConfirmationHost } from './confirmation-host.component';

describe('CsConfirmationHost', () => {
  let fixture: ComponentFixture<CsConfirmationHost>;
  let confirmations: ConfirmationService;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [ConfirmationService] });
    confirmations = TestBed.inject(ConfirmationService);
    fixture = TestBed.createComponent(CsConfirmationHost);
    fixture.detectChanges();
  });

  function host(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  async function open(request: Parameters<ConfirmationService['confirm']>[0]) {
    const answered: (boolean | null)[] = [null];
    confirmations.confirm(request).subscribe((accepted) => (answered[0] = accepted));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return answered;
  }

  it('AC807_2_EscapeCancelsTheDialog: closes and resolves false', async () => {
    const answered = await open({ title: 'Delete', message: 'Delete this?', danger: true });
    expect(host().querySelector('[role="alertdialog"]')).not.toBeNull();

    const backdrop = host().querySelector('div')!;
    backdrop.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(answered[0]).toBe(false);
    expect(host().querySelector('[role="alertdialog"]')).toBeNull();
  });

  it('AC807_2_OtherKeysDoNotCloseTheDialog: only Escape dismisses', async () => {
    const answered = await open({ title: 'Delete', message: 'Delete this?' });

    const backdrop = host().querySelector('div')!;
    backdrop.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();

    expect(answered[0]).toBeNull();
    expect(host().querySelector('[role="alertdialog"]')).not.toBeNull();
  });

  it('AC807_3_FocusMovesToCancelForADangerRequest', async () => {
    await open({ title: 'Delete', message: 'Delete this?', danger: true });

    const cancel = host().querySelector<HTMLButtonElement>('[data-testid="confirm-cancel"]')!;
    expect(document.activeElement).toBe(cancel);
  });

  it('AC807_3_FocusReturnsToTheTriggerOnClose', async () => {
    // A real trigger the user pressed before the dialog opened.
    const trigger = document.createElement('button');
    trigger.textContent = 'Deactivate';
    document.body.appendChild(trigger);
    trigger.focus();
    expect(document.activeElement).toBe(trigger);

    try {
      await open({ title: 'Delete', message: 'Delete this?', danger: true });
      expect(document.activeElement).not.toBe(trigger);

      confirmations.resolve(false);
      fixture.detectChanges();
      await fixture.whenStable();

      expect(document.activeElement).toBe(trigger);
    } finally {
      trigger.remove();
    }
  });

  it('AC807_4_RendersDetailsAsAList', async () => {
    await open({
      title: 'Apply changes',
      message: '2 changes',
      details: ['Grant ticket.close → Agent', 'Revoke report.export → Supervisor'],
    });

    const items = host().querySelectorAll('[data-testid="confirm-details"] li');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('Grant ticket.close → Agent');
    expect(items[1].textContent).toContain('Revoke report.export → Supervisor');
  });

  it('AC807_4_OmitsTheListWhenThereAreNoDetails', async () => {
    await open({ title: 'Delete', message: 'Delete this?' });

    expect(host().querySelector('[data-testid="confirm-details"]')).toBeNull();
  });

  it('AC807_1_ShowsTheNextQueuedRequestAfterOneResolves', async () => {
    await open({ title: 'First', message: 'First?' });
    confirmations.confirm({ title: 'Second', message: 'Second?' }).subscribe();
    fixture.detectChanges();

    confirmations.resolve(true);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(host().textContent).toContain('Second');
    expect(host().textContent).not.toContain('First?');
  });
});
```

`data-testid` hooks are used for the two controls because the existing markup has no stable
selector for them and asserting on translated button text would make the test a copy check rather
than a behaviour check.

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd frontend && npx ng test common --watch=false --include='**/confirmation-host.component.spec.ts'
```

Expected: all seven fail — no `data-testid` attributes, no Escape handling, no focus management, no
details list.

- [ ] **Step 3: Rewrite the component**

Replace `confirmation-host.component.ts`:

```ts
import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, viewChild } from '@angular/core';
import { TranslatePipe } from '../i18n/translate.pipe';
import { ConfirmationService } from './confirmation.service';
import { CsIcon } from './icon.component';

/**
 * Renders the head of the confirmation queue (`ConfirmationService.current()`).
 *
 * Mounted once per app, in the shell (`shell.component.html:275`), so every screen shares one
 * dialog implementation rather than each rolling its own with its own RTL, keyboard and
 * screen-reader behaviour.
 *
 * Keyboard handling mirrors `CsDialog` (`dialog.component.ts:32-50`): focus moves in on open via
 * `effect` + `queueMicrotask`, and Escape is caught on the backdrop — safe here because focus is
 * always inside the panel by then, so the keydown bubbles out to it.
 */
@Component({
  selector: 'cs-confirmation-host',
  imports: [CsIcon, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './confirmation-host.component.html',
})
export class CsConfirmationHost {
  readonly confirmations = inject(ConfirmationService);

  private readonly cancelButton = viewChild<ElementRef<HTMLButtonElement>>('cancelButton');
  private readonly confirmButton = viewChild<ElementRef<HTMLButtonElement>>('confirmButton');

  /** Where focus was when the dialog opened, so it can be given back (AC-807.3). */
  private trigger: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const request = this.confirmations.current();
      // Both view children are read so this effect re-runs once they exist — on the first pass
      // after `current()` becomes non-null the buttons have not been created yet.
      const cancel = this.cancelButton();
      const confirm = this.confirmButton();

      if (!request) {
        const trigger = this.trigger;
        this.trigger = null;
        if (trigger?.isConnected) {
          queueMicrotask(() => trigger.focus());
        }
        return;
      }

      if (!this.trigger) {
        const active = document.activeElement;
        this.trigger = active instanceof HTMLElement ? active : null;
      }

      // Cancel for a destructive request: Enter must not become the destructive act (AC-807.3).
      const target = request.danger ? cancel : (confirm ?? cancel);
      queueMicrotask(() => target?.nativeElement.focus());
    });
  }

  /** Escape and backdrop click both mean "no" — the safe answer to a question about deleting. */
  cancel(): void {
    this.confirmations.resolve(false);
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.stopPropagation();
      this.cancel();
    }
  }
}
```

- [ ] **Step 4: Rewrite the template**

Replace `confirmation-host.component.html`. The changes against the current file are: `(keydown)` on
the backdrop, `#cancelButton` / `#confirmButton` refs with `data-testid`s, and the `details` block.

```html
@if (confirmations.current(); as request) {
  <div
    class="fixed inset-0 z-[60] grid place-items-center bg-on-surface/40 p-4"
    (click)="cancel()"
    (keydown)="onKeydown($event)"
  >
    <section
      role="alertdialog"
      aria-modal="true"
      [attr.aria-labelledby]="'confirm-title-' + request.id"
      [attr.aria-describedby]="'confirm-message-' + request.id"
      class="w-full max-w-md rounded-lg border border-border-subtle bg-surface-lowest shadow-popover"
      (click)="$event.stopPropagation()"
    >
      <div class="flex items-start gap-3 border-b border-border-subtle px-5 py-4">
        <span
          class="grid size-9 shrink-0 place-items-center rounded-lg"
          [class.bg-error-container]="request.danger"
          [class.text-on-error-container]="request.danger"
          [class.bg-primary-container]="!request.danger"
          [class.text-on-primary-container]="!request.danger"
        >
          <cs-icon [name]="request.danger ? 'warning' : 'help'" [size]="20" />
        </span>
        <div class="min-w-0">
          <h2 [id]="'confirm-title-' + request.id" class="text-headline-md text-on-surface">
            {{ request.title }}
          </h2>
          <p [id]="'confirm-message-' + request.id" class="mt-1 text-body-md text-on-surface-variant">
            {{ request.message }}
          </p>

          @if (request.details?.length) {
            <ul
              data-testid="confirm-details"
              class="mt-3 max-h-48 space-y-1 overflow-y-auto rounded-lg bg-surface-low px-3 py-2 text-body-sm text-on-surface-variant"
            >
              @for (detail of request.details; track detail) {
                <li class="flex gap-2 font-data-mono text-xs">
                  <span aria-hidden="true">·</span>
                  <span>{{ detail }}</span>
                </li>
              }
            </ul>
          }
        </div>
      </div>

      <div class="flex justify-end gap-2 px-5 py-4">
        <button
          #cancelButton
          data-testid="confirm-cancel"
          type="button"
          class="rounded-lg border border-outline-variant bg-surface-lowest px-4 py-2 text-label-lg font-semibold text-on-surface transition-colors hover:bg-surface-high"
          (click)="cancel()"
        >
          {{ request.cancelText ?? ('action.cancel' | t) }}
        </button>
        <button
          #confirmButton
          data-testid="confirm-accept"
          type="button"
          class="rounded-lg px-4 py-2 text-label-lg font-semibold transition-colors"
          [class.bg-error]="request.danger"
          [class.text-on-error]="request.danger"
          [class.bg-primary]="!request.danger"
          [class.text-on-primary]="!request.danger"
          (click)="confirmations.resolve(true)"
        >
          {{ request.confirmText ?? ('action.confirm' | t) }}
        </button>
      </div>
    </section>
  </div>
}
```

`@for (detail of request.details; track detail)` tracks by value: the list is short, static for the
life of the dialog, and duplicate entries are impossible (a change is either a grant or a revoke of
one role/permission pair, never both).

- [ ] **Step 5: Run the tests to verify they pass**

```bash
cd frontend && npx ng test common --watch=false --include='**/confirmation-host.component.spec.ts'
```

Expected: PASS, 7 tests. Paste the output below.

**If `AC807_3_FocusReturnsToTheTriggerOnClose` is flaky**, the cause is the `queueMicrotask`
boundary, not the assertion: add a second `await fixture.whenStable()` in the test rather than
replacing `queueMicrotask` with a timeout. `CsDialog` sets the precedent for the microtask
(`dialog.component.ts:37`) and a timer would make every consumer's test need `fakeAsync`.

- [ ] **Step 6: Run the whole `common` suite and build**

```bash
cd frontend && npx ng test common --watch=false && npx ng build admin-app
```

Expected: no regressions. `kb-admin.component.ts:335-352` passes no `details` and is covered by the
"omits the list" test.

- [ ] **Step 7: Commit**

```bash
git add frontend/projects/common/src/lib/ui/confirmation-host.component.ts \
        frontend/projects/common/src/lib/ui/confirmation-host.component.html \
        frontend/projects/common/src/lib/ui/confirmation-host.component.spec.ts
git commit -m "fix: make the confirmation dialog keyboard-dismissable and render details (AC-807.2..AC-807.4)"
```

## Criteria covered

`AC-807.2`, `AC-807.3`, `AC-807.4`.

## Test evidence

Implemented 2026-09-01:

```
npx ng test common --watch=false --include='**/confirmation*.spec.ts'
Test Files  2 passed (2)
     Tests  12 passed (12)
```

`kb-admin.component.spec.ts` (the existing, only prior consumer of the dialog) also re-run in
isolation and still passes: `Test Files 1 passed (1)`, `Tests 4 passed (4)` — confirms the
`details`/queue/focus changes are backwards compatible.

## Deviations from the plan

None.
