import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ApiError,
  CsButton,
  CsCard,
  CsIcon,
  CsInputField,
  FieldError,
  LocaleStore,
  TranslatePipe,
  WebFormApi,
} from 'common';

@Component({
  selector: 'portal-web-form',
  imports: [CsCard, CsIcon, CsButton, CsInputField, ReactiveFormsModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './web-form.component.html',
})
export default class WebFormComponent {
  private readonly api = inject(WebFormApi);
  protected readonly locale = inject(LocaleStore);

  readonly submitting = signal(false);
  readonly submittedReference = signal<string | null>(null);
  readonly error = signal<ApiError | null>(null);

  readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    subject: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(150)],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    // Honeypot field for bot detection (FB-7 / CC-22)
    website: new FormControl('', { nonNullable: true }),
  });

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    const { name, email, subject, description, website } = this.form.getRawValue();

    // If honeypot is populated by a bot, fake immediate success (FB-7 / CC-22)
    if (website.trim().length > 0) {
      this.submittedReference.set(`TICK-${Math.floor(100000 + Math.random() * 900000)}`);
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    this.api
      .submit({
        name,
        email,
        subject,
        description,
      })
      .subscribe({
        next: (res) => {
          this.submitting.set(false);
          this.submittedReference.set(res.reference);
        },
        error: (failure: unknown) => {
          this.submitting.set(false);
          this.error.set(
            failure instanceof ApiError
              ? failure
              : new ApiError('ERR_SUBMIT', 'Submission failed', [], '', 0),
          );
        },
      });
  }

  reset(): void {
    this.form.reset();
    this.submittedReference.set(null);
    this.error.set(null);
  }

  fieldError(field: string): FieldError | null {
    return this.error()?.fieldError(field) ?? null;
  }
}
