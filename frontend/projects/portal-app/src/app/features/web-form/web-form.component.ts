import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ApiError,
  toLocalizedApiError,
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

  /** The bare textarea has no cs-input-field to render its own error, so the template asks here. */
  descriptionInvalid(): boolean {
    const control = this.form.controls.description;
    return control.invalid && (control.touched || control.dirty);
  }

  submit(): void {
    if (this.submitting()) {
      return;
    }

    // The submit button is deliberately never disabled: pressing it on an incomplete form is how a
    // visitor finds out which field is missing. Marking everything touched is what makes each
    // cs-input-field (and the textarea above) show its own message.
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, email, subject, description, website } = this.form.getRawValue();
    const honeypot = website.trim();

    this.submitting.set(true);
    this.error.set(null);

    // The honeypot is sent, not judged here (CC-22/CC-47). Deciding locally used to fake a
    // reference and skip the request entirely, which meant a browser autofilling the hidden input
    // silently discarded a real customer's ticket — and it never protected anything, because a bot
    // posts straight to the endpoint and never runs this code. The server answers a honeypot-filled
    // or rate-limited submission indistinguishably from a real one, so there is nothing to branch on.
    this.api
      .submit({
        name,
        email,
        subject,
        description,
        ...(honeypot.length > 0 ? { honeypot } : {}),
      })
      .subscribe({
        next: (res) => {
          this.submitting.set(false);
          this.submittedReference.set(res.reference);
        },
        error: (failure: unknown) => {
          this.submitting.set(false);
          this.error.set(
            toLocalizedApiError(failure, this.locale),
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
