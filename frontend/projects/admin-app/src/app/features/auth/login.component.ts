import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ApiError,
  AuthApi,
  CsButton,
  CsCard,
  CsIcon,
  CsInputField,
  LocaleStore,
  SessionStore,
  TranslatePipe,
} from 'common';

/**
 * Sign-in.
 *
 * Navigation happens on the success path and nowhere else: a form that
 * navigates and then bounces back shows a flash of the protected page, which
 * reads as a bug even when nothing was exposed (AC-55).
 *
 * `busy` and `error` are separate signals rather than one boolean with an
 * error field beside it, because a boolean cannot hold the failure and the two
 * then drift into contradicting each other.
 */
@Component({
  selector: 'admin-login',
  imports: [ReactiveFormsModule, CsCard, CsIcon, CsInputField, CsButton, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.component.html',
})
export default class LoginComponent {
  private readonly api = inject(AuthApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly locale = inject(LocaleStore);

  readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  readonly busy = signal(false);
  readonly error = signal<ApiError | null>(null);

  /**
   * Where the guard wanted to go, if it interrupted one. Otherwise the dashboard, which is where
   * MVP-12 moved the landing page: the queue unfiltered is a place you go to look something up,
   * not where an agent's day starts.
   */
  readonly returnUrl = signal(this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard');

  submit(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }


    this.busy.set(true);
    this.error.set(null);

    const { email, password } = this.form.getRawValue();

    this.api.signIn(email, password).subscribe({
      next: (result) => {
        this.session.signIn(result);
        this.busy.set(false);
        void this.router.navigateByUrl(this.returnUrl());
      },
      error: (failure: unknown) => {
        this.busy.set(false);
        this.error.set(
          failure instanceof ApiError
            ? failure
            : new ApiError(
                'ERR_UNKNOWN',
                'Something went wrong',
                [],
                '',
                0,
              ),
        );
      },
    });
  }
}
