import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  ApiError,
  CsActionBar,
  CsButton,
  CsCard,
  CsIcon,
  CsInputField,
  CustomerApi,
  LocaleStore,
  TranslatePipe,
} from 'common';

/**
 * MVP-03 — the create-customer form. `AC-70`.
 *
 * Two failure shapes reach this screen and they must not be rendered alike:
 *
 *  - a field-keyed **400** from `CreateCustomerCommandValidator`, which names a control and belongs
 *    under it;
 *  - a **409** `CUSTOMER_EMAIL_EXISTS`, which names no field because the request is well formed and
 *    it is the state of the world that refuses it. It has no control to attach to, so it renders at
 *    form level.
 *
 * Putting the conflict on the email control would be the closer guess visually and the wrong one:
 * the value the user typed is valid, and marking it invalid says otherwise.
 */
@Component({
  selector: 'admin-customer-create',
  imports: [ReactiveFormsModule, RouterLink, CsActionBar, CsCard, CsIcon, CsInputField, CsButton, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './customer-create.component.html',
})
export default class CustomerCreateComponent {
  private readonly api = inject(CustomerApi);
  private readonly router = inject(Router);

  protected readonly locale = inject(LocaleStore);

  readonly saving = signal(false);
  readonly submitError = signal<ApiError | null>(null);

  /**
   * Client rules mirroring `CreateCustomerCommandValidator` — 200, 320 and 32 because the validator
   * says so, not because the inputs looked about right. Where the two disagree the server wins, and
   * AC-70's field-keyed path is what shows the user why.
   */
  readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(320)],
    }),
    phone: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(32)],
    }),
  });

  /** A failure with no field has no control to attach to, so it renders at form level (AC-70). */
  readonly formLevelError = computed(() => {
    const failure = this.submitError();
    return failure && !failure.hasFieldErrors ? failure : null;
  });

  submit(): void {
    if (this.form.invalid || this.saving()) {
      // Nothing leaves while the form is invalid, and nothing leaves twice.
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.submitError.set(null);

    const { name, email, phone } = this.form.getRawValue();

    this.api
      // An untouched optional field is `''` in a non-nullable control; the backend models "no
      // phone" as null, and sending an empty string would store a phone number that is not one.
      .create({ name, email, phone: phone.trim() === '' ? null : phone })
      .subscribe({
        next: (created) => {
          this.saving.set(false);
          // AC-70 — creation lands on the new customer's detail screen, not back on the list.
          void this.router.navigateByUrl(`/customers/${created.id}`);
        },
        error: (error: unknown) => {
          this.saving.set(false);
          this.submitError.set(this.toApiError(error));
        },
      });
  }

  /** AC-70 — the server error for one control, by the field name the server used. */
  fieldError(field: string) {
    return this.submitError()?.fieldError(field) ?? null;
  }

  /**
   * Clears a server error once the user edits the control it points at. Not spelled out by any
   * criterion — it is ordinary correctness. Leaving it would keep a corrected field showing the old
   * rejection, and the form would look broken.
   */
  clearServerError(field: string): void {
    const failure = this.submitError();
    if (!failure?.fieldError(field)) {
      return;
    }

    const remaining = failure.errors.filter((error) => error.field !== field);
    this.submitError.set(
      new ApiError(failure.code, failure.message_, remaining, failure.traceId, failure.status),
    );
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError
      ? error
      : new ApiError(
          'ERR_UNKNOWN',
          'Something went wrong',
          [],
          '',
          0,
        );
  }
}
