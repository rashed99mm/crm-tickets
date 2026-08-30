import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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

@Component({
  selector: 'portal-login',
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
  templateUrl: './login.component.html',
})
export default class PortalLoginComponent {
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

  readonly returnUrl = signal(
    this.route.snapshot.queryParamMap.get('returnUrl') ?? '/app',
  );

  // ASG-7: set when the signup flow registers the user but the automatic sign-in fails, so the
  // login screen can say the account exists rather than making the visitor wonder what happened.
  readonly accountCreated = signal(this.route.snapshot.queryParamMap.get('created') === '1');

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
            : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0),
        );
      },
    });
  }
}
