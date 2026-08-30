import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { FieldError } from '../api/api-response';
import { LocaleStore } from '../i18n/locale.store';

let nextId = 0;

/**
 * A labelled text input with validation display.
 *
 * The entire validation visual language is designed here — no mockup in the
 * set shows an error state, a required marker, or `aria-invalid` anywhere,
 * including the one full form screen.
 *
 * Two rules, deliberately different:
 *
 *  - A CLIENT error appears only after the control is touched or dirty, so a
 *    form the user has not filled in is not a wall of red (AC-59).
 *  - A SERVER error appears immediately regardless. The request was already
 *    rejected; hiding the reason until the user pokes that particular field
 *    is worse than useless (AC-60).
 */
@Component({
  selector: 'cs-input-field',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './input-field.component.html',
})
export class CsInputField {
  private readonly locale = inject(LocaleStore);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly label = input.required<string>();
  readonly control = input.required<FormControl>();
  readonly serverError = input<FieldError | null>(null);
  readonly type = input('text');

  protected readonly id = `cs-input-${nextId++}`;
  protected readonly errorId = `${this.id}-error`;

  constructor() {
    effect(() => {
      const ctrl = this.control();
      const sub1 = ctrl.statusChanges.subscribe(() => this.cdr.markForCheck());
      const sub2 = ctrl.valueChanges.subscribe(() => this.cdr.markForCheck());
      const sub3 = ctrl.events?.subscribe(() => this.cdr.markForCheck());
      this.destroyRef.onDestroy(() => {
        sub1?.unsubscribe();
        sub2?.unsubscribe();
        sub3?.unsubscribe();
      });
    });
  }

  showError(): boolean {
    if (this.serverError()) {
      return true;
    }

    const control = this.control();
    return control.invalid && (control.touched || control.dirty);
  }

  protected borderTone(): string {
    return this.showError() ? 'border-error' : 'border-outline-variant focus:border-primary';
  }


  errorText(): string {
    const server = this.serverError();
    if (server) {
      return server.message;
    }

    const errors = this.control().errors ?? {};

    // AC-63 — client validation text comes from the dictionary, not from literals here. These are
    // the only user-facing strings the library owns that no server ever sends, and they are the
    // ones most easily forgotten: they live in TypeScript, so the template sweep cannot see them.
    if (errors['required']) {
      return this.locale.t('validation.required');
    }
    if (errors['email']) {
      return this.locale.t('validation.email');
    }
    if (errors['maxlength']) {
      return this.locale.t('validation.maxlength', errors['maxlength'].requiredLength);
    }
    if (errors['minlength']) {
      return this.locale.t('validation.minlength', errors['minlength'].requiredLength);
    }
    if (errors['pattern']) {
      return this.locale.t('validation.pattern');
    }

    return this.locale.t('validation.invalid');

  }
}
