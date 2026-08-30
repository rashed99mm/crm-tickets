import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ApiError,
  CsBadge,
  CsButton,
  CsCard,
  CsErrorState,
  CsIcon,
  CsInputField,
  CsLoadingState,
  CsPlaceholder,
  FieldError,
  LocaleStore,
  SessionStore,
  StaffApi,
  StaffProfile,
  TranslatePipe,
  UploadService,
} from 'common';

/**
 * The signed-in user's profile and settings workspace adapted from the Stitch reference.
 * Provides self-service general profile updating, phone OTP verification, and password change.
 */
@Component({
  selector: 'admin-profile',
  imports: [
    CsCard,
    CsIcon,
    ReactiveFormsModule,
    CsInputField,
    CsButton,
    CsLoadingState,
    CsPlaceholder,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile.component.html',
})
export default class ProfileComponent implements OnInit {
  private readonly api = inject(StaffApi);
  private readonly session = inject(SessionStore);
  private readonly uploadService = inject(UploadService);

  protected readonly locale = inject(LocaleStore);
  protected readonly displayName = this.session.displayName;
  protected readonly roles = this.session.roles;

  readonly activeTab = signal<'general' | 'security' | 'notifications' | 'billing'>('general');
  readonly profile = signal<StaffProfile | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly uploadingPhoto = signal(false);
  readonly error = signal<ApiError | null>(null);
  readonly saved = signal(false);

  readonly passwordBusy = signal(false);
  readonly passwordError = signal<ApiError | null>(null);
  readonly passwordDone = signal(false);

readonly requiresPhoneVerification = signal(false);
  readonly pendingVerificationId = signal<string | null>(null);
  readonly otpBusy = signal(false);
  readonly otpDone = signal(false);
  readonly otpError = signal<FieldError | null>(null);

  readonly profileForm = new FormGroup({
    firstName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    lastName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    phoneNumber: new FormControl<string | null>(null),
    profileImageUrl: new FormControl<string | null>(null),
  });

  readonly passwordForm = new FormGroup({
    currentPassword: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    newPassword: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(12)],
    }),
  });

  readonly otpForm = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^[0-9]{6}$/)],
    }),
  });

  ngOnInit(): void {
    this.loadProfile();
  }

  loadProfile(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.getCurrentProfile().subscribe({
      next: (data) => {
        this.profile.set(data);
        this.profileForm.patchValue({
          firstName: data?.firstName ?? '',
          lastName: data?.lastName ?? '',
          phoneNumber: data?.phoneNumber ?? null,
          profileImageUrl: data?.profileImageUrl ?? null,
        });
        this.loading.set(false);
      },
      error: (failure: unknown) => {
        this.loading.set(false);
        this.error.set(this.toApiError(failure));
      },
    });
  }

  saveProfile(): void {
    if (this.profileForm.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.saved.set(false);

    const previousPhone = this.profile()?.phoneNumber;
    const formValue = this.profileForm.getRawValue();

this.api.updateCurrentProfile(formValue).subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.saved.set(true);
        this.profile.set(updated);

        const phoneChanged = updated?.phoneNumber !== previousPhone;
        const needsVerification =
          Boolean(phoneChanged && updated?.phoneNumber && !updated?.phoneNumberConfirmed);
        this.requiresPhoneVerification.set(needsVerification);
        if (needsVerification) {
          this.otpForm.reset();
          this.otpDone.set(false);
          this.otpError.set(null);
          this.requestOtp(updated.phoneNumber!);
        }
      },
      error: (failure: unknown) => {
        this.busy.set(false);
        this.error.set(this.toApiError(failure));
      },
    });
  }

  onPhotoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) {
      return;
    }
    const file = input.files[0];
    const validation = this.uploadService.validateImage(file);
    if (!validation.valid) {
      return;
    }

    this.uploadingPhoto.set(true);
    this.uploadService.uploadProfileImage(file).subscribe({
      next: (res) => {
        this.uploadingPhoto.set(false);
        this.profileForm.controls.profileImageUrl.setValue(res.url);
        this.profileForm.controls.profileImageUrl.markAsDirty();
      },
      error: () => {
        this.uploadingPhoto.set(false);
      },
    });
  }

  removePhoto(): void {
    this.profileForm.controls.profileImageUrl.setValue(null);
    this.profileForm.controls.profileImageUrl.markAsDirty();
  }

  cancelEdit(): void {
    const current = this.profile();
    if (current) {
      this.profileForm.patchValue({
        firstName: current.firstName ?? '',
        lastName: current.lastName ?? '',
        phoneNumber: current.phoneNumber ?? null,
        profileImageUrl: current.profileImageUrl ?? null,
      });
    }
    this.error.set(null);
    this.saved.set(false);
  }

  verifyPhone(): void {
    if (this.otpForm.invalid || this.otpBusy()) {
      return;
    }

    this.otpBusy.set(true);
    this.otpError.set(null);

const { code } = this.otpForm.getRawValue();
    this.api.verifyOtp({ verificationId: this.pendingVerificationId() ?? '', code }).subscribe({
      next: () => {
        this.otpBusy.set(false);
        this.otpDone.set(true);
        this.requiresPhoneVerification.set(false);
        this.loadProfile();
      },
      error: (failure: unknown) => {
        this.otpBusy.set(false);
        const apiErr = this.toApiError(failure);
        this.otpError.set(
          apiErr.fieldError('code') ?? {
            field: 'code',
            code: apiErr.code,
            message: apiErr.message_,
          },
        );
      },
    });
  }

resendOtp(): void {
    const phone = this.profile()?.phoneNumber;
    if (!phone || this.otpBusy()) {
      return;
    }

    this.requestOtp(phone);
  }

  private requestOtp(phone: string): void {
    this.otpBusy.set(true);
    this.otpError.set(null);

    this.api.requestPhoneVerification(phone).subscribe({
      next: (response) => {
        this.otpBusy.set(false);
        this.pendingVerificationId.set(response.verificationId);
      },
      error: (failure: unknown) => {
        this.otpBusy.set(false);
        const apiErr = this.toApiError(failure);
        this.otpError.set({
          field: 'code',
          code: apiErr.code,
          message: apiErr.message_,
        });
      },
    });
  }

  changePassword(): void {
    if (this.passwordForm.invalid || this.passwordBusy()) {
      return;
    }

    this.passwordBusy.set(true);
    this.passwordError.set(null);
    this.passwordDone.set(false);

    const { currentPassword, newPassword } = this.passwordForm.getRawValue();

    this.api.changeOwnPassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.passwordBusy.set(false);
        this.passwordDone.set(true);
        this.passwordForm.reset();
      },
      error: (failure: unknown) => {
        this.passwordBusy.set(false);
        this.passwordError.set(this.toApiError(failure));
      },
    });
  }

  fieldError(field: string): FieldError | null {
    return this.error()?.fieldError(field) ?? null;
  }

  passwordFieldError(field: string): FieldError | null {
    return this.passwordError()?.fieldError(field) ?? null;
  }

  setTab(tab: 'general' | 'security' | 'notifications' | 'billing'): void {
    this.activeTab.set(tab);
  }

  private toApiError(failure: unknown): ApiError {
    return failure instanceof ApiError
      ? failure
      : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
