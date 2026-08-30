import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  ApiError,
  AuthApi,
  CsButton,
  CsCard,
  CsIcon,
  CsInputField,
  CsLanguageSwitcher,
  LocaleStore,
  SessionStore,
  TranslatePipe,
} from 'common';

/**
 * Portal sign-up (`US-401`/`ASG-3`..`ASG-7`).
 *
 * Success is register → sign in → /app, because `POST /api/Auth/register` returns only the new
 * user's id, not tokens (spec A2). The two failure shapes are rendered differently, matching
 * `customer-create`: a field-keyed 400 lands under its control; a 409 conflict (`EMAIL_EXISTS` /
 * `USERNAME_EXISTS`) names no field and renders at form level (ASG-6).
 *
 * A register that succeeds but whose automatic sign-in fails is *still* a successful registration,
 * so it routes to /login with an account-created cue rather than reporting the whole flow as failed
 * (ASG-7).
 */
@Component({
  selector: 'portal-signup',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    CsCard,
    CsIcon,
    CsInputField,
    CsButton,
    CsLanguageSwitcher,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './signup.component.html',
})
export default class PortalSignupComponent {
  private readonly api = inject(AuthApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);

  protected readonly locale = inject(LocaleStore);

  readonly saving = signal(false);
  readonly submitError = signal<ApiError | null>(null);

  /**
   * Client rules mirror `RegisterCommandValidator` (spec A4): email required+valid+≤255; username
   * required, 3–50, letters/digits/underscore; password required, ≥8, one lowercase/uppercase/digit;
   * names required ≤100; phone optional. Where they disagree the server wins, and the field-keyed
   * path shows why.
   */
  readonly form = new FormGroup({
    firstName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    lastName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(255)],
    }),
    username: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(50),
        Validators.pattern(/^[a-zA-Z0-9_]+$/),
      ],
    }),
    phone: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(20)],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.minLength(8),
        Validators.maxLength(100),
        Validators.pattern(/[A-Z]/),
        Validators.pattern(/[a-z]/),
        Validators.pattern(/[0-9]/),
      ],
    }),
  });

  /** A failure with no field (a 409 conflict) has no control to attach to, so it renders at form level. */
  readonly formLevelError = computed(() => {
    const failure = this.submitError();
    return failure && !failure.hasFieldErrors ? failure : null;
  });

  submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.submitError.set(null);

    const { firstName, lastName, email, username, phone, password } = this.form.getRawValue();

    this.api
      .register({
        email,
        username,
        password,
        firstName,
        lastName,
        // Blank phone travels as null, never "" (spec A4).
        phoneNumber: phone.trim() === '' ? null : phone.trim(),
      })
      .subscribe({
        next: () => this.signInAfterRegister(email, password),
        error: (error: unknown) => {
          this.saving.set(false);
          this.submitError.set(this.toApiError(error));
        },
      });
  }

  private signInAfterRegister(email: string, password: string): void {
    this.api.signIn(email, password).subscribe({
      next: (result) => {
        this.session.signIn(result);
        this.saving.set(false);
        void this.router.navigateByUrl('/app');
      },
      error: () => {
        // ASG-7: the account exists; only the automatic sign-in failed. Don't report the whole
        // registration as failed.
        this.saving.set(false);
        void this.router.navigate(['/login'], { queryParams: { created: '1' } });
      },
    });
  }

  /** ASG-6 — the server error for one control, by the field name the server used. */
  fieldError(field: string) {
    return this.submitError()?.fieldError(field) ?? null;
  }

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
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
