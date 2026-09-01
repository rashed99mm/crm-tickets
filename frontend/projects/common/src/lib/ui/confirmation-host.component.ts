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
