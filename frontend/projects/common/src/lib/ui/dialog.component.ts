import { ChangeDetectionStrategy, Component, ElementRef, effect, input, output, viewChild } from '@angular/core';
import { CsIcon } from './icon.component';
import { TranslatePipe } from '../i18n/translate.pipe';

/**
 * A modal dialog: backdrop + panel, closing on backdrop click or Escape.
 *
 * Replaces the always-visible inline "create" forms several admin screens used to render above
 * their own list — the same form now opens on demand instead of permanently occupying the page.
 *
 * ```html
 * <cs-dialog [open]="showCreate()" [heading]="'departments.create.title' | t" (closed)="showCreate.set(false)">
 *   <form …>…</form>
 * </cs-dialog>
 * ```
 *
 * `heading` is already localised by the caller, matching `cs-card`'s own convention.
 */
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
    // Moves focus into the dialog the moment it opens — a background click target left focused
    // behind an overlay is a keyboard trap in the other direction.
    effect(() => {
      if (this.open()) {
        queueMicrotask(() => this.panel()?.nativeElement.focus());
      }
    });
  }

  dismiss(): void {
    this.closed.emit();
  }

  onBackdropKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.dismiss();
    }
  }
}
