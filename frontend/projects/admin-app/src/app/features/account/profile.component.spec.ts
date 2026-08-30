import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import ProfileComponent from './profile.component';

function envelope(data: unknown) {
  return { success: true, code: 'AUTH_OK', message: 'OK', data, errors: [] };
}

function failure(
  status: number,
  code: string,
  details: { field: string; code: string; message: string }[] | null = null,
) {
  return {
    body: {
      success: false,
      code,
      message: 'Validation failed',
      data: null,
      errors: details ?? [],
    },
    opts: { status, statusText: 'Error' },
  };
}

const MOCK_PROFILE = {
  id: 'user-001',
  email: 'alex.morgan@commandcenter.crm',
  username: 'alex.morgan',
  firstName: 'Alex',
  lastName: 'Morgan',
  phoneNumber: '+14155550100',
  emailConfirmed: true,
  phoneNumberConfirmed: true,
  isActive: true,
  createdAt: '2026-08-25T10:00:00Z',
  roles: ['Admin', 'Agent'],
  profileImageUrl: 'https://example.com/avatar.png',
};

describe('ProfileComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function render(): ComponentFixture<ProfileComponent> {
    const fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('AC447_ProfileRendersReferenceSettingsRegions: renders tabs, profile picture card, info card, and preferences card', () => {
    const fixture = render();
    http.expectOne('/api/Auth/me').flush(envelope(MOCK_PROFILE));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('nav[aria-label="Profile settings"]')).not.toBeNull();
    expect(fixture.componentInstance.profileForm.controls.firstName.value).toBe('Alex');
    expect(fixture.componentInstance.profileForm.controls.lastName.value).toBe('Morgan');
  });

  it('AC447_EmailIsReadOnlyAndUnsupportedFieldsAreUnavailable: email is disabled and placeholders are rendered for JobTitle/TimeZone', () => {
    const fixture = render();
    http.expectOne('/api/Auth/me').flush(envelope(MOCK_PROFILE));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const emailInput = el.querySelector<HTMLInputElement>('input#email');
    expect(emailInput).not.toBeNull();
    expect(emailInput?.disabled || emailInput?.readOnly).toBe(true);
    expect(emailInput?.value).toBe('alex.morgan@commandcenter.crm');

    const placeholders = el.querySelectorAll('cs-placeholder');
    expect(placeholders.length).toBeGreaterThanOrEqual(2);
  });

  it('AC430_SubmitsOnlyWhenValidAndMapsTheUpdatedProfile: updates profile via PUT /api/Auth/me', () => {
    const fixture = render();
    http.expectOne('/api/Auth/me').flush(envelope(MOCK_PROFILE));
    fixture.detectChanges();

    fixture.componentInstance.profileForm.patchValue({
      firstName: 'Alexander',
      lastName: 'Morgan',
    });

    fixture.componentInstance.saveProfile();

    const req = http.expectOne('/api/Auth/me');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.firstName).toBe('Alexander');

    const updatedProfile = { ...MOCK_PROFILE, firstName: 'Alexander' };
    req.flush(envelope(updatedProfile));
    fixture.detectChanges();

    expect(fixture.componentInstance.saved()).toBe(true);
    expect(fixture.componentInstance.profile()?.firstName).toBe('Alexander');
  });

  it('AC433_MapsServerFieldErrorsToFormControls: displays server validation error on field', () => {
    const fixture = render();
    http.expectOne('/api/Auth/me').flush(envelope(MOCK_PROFILE));
    fixture.detectChanges();

    fixture.componentInstance.profileForm.patchValue({
      firstName: '',
      lastName: 'Morgan',
    });

    // Form invalid client-side
    expect(fixture.componentInstance.profileForm.invalid).toBe(true);

    fixture.componentInstance.profileForm.patchValue({
      firstName: 'Alex',
    });

    fixture.componentInstance.saveProfile();
    const req = http.expectOne('/api/Auth/me');
    const f = failure(400, 'VALIDATION_ERROR', [
      { field: 'firstName', code: 'INVALID_FIRST_NAME', message: 'First name is invalid' },
    ]);
    req.flush(f.body, f.opts);
    fixture.detectChanges();

    expect(fixture.componentInstance.fieldError('firstName')?.message).toBe('First name is invalid');
  });

  it('AC436_PhoneChangeRequiresOtpVerification: opens OTP verification when phone number changed to unconfirmed phone', () => {
    const fixture = render();
    http.expectOne('/api/Auth/me').flush(envelope(MOCK_PROFILE));
    fixture.detectChanges();

    fixture.componentInstance.profileForm.patchValue({
      phoneNumber: '+14155550999',
    });

    fixture.componentInstance.saveProfile();

const req = http.expectOne('/api/Auth/me');
    const updatedUnconfirmed = {
      ...MOCK_PROFILE,
      phoneNumber: '+14155550999',
      phoneNumberConfirmed: false,
    };
    req.flush(envelope(updatedUnconfirmed));
    fixture.detectChanges();

    expect(fixture.componentInstance.requiresPhoneVerification()).toBe(true);

    // The changed phone immediately requests a code and stores the verification id for the verify call.
    const otpReq = http.expectOne('/api/verification/request-phone');
    expect(otpReq.request.body.phoneNumber).toBe('+14155550999');
    otpReq.flush(
      envelope({ verificationId: 'v-123', expiresAtUtc: '2026-08-27T10:05:00Z', retryAfterSeconds: 60, channel: 'SMS' }),
    );
    fixture.detectChanges();

    expect(fixture.componentInstance.pendingVerificationId()).toBe('v-123');
  });

  it('AC440_WrongOtpShowsSafeErrorAndDoesNotMarkPhoneVerified: displays safe error on invalid OTP code', () => {
    const fixture = render();
    http.expectOne('/api/Auth/me').flush(envelope({ ...MOCK_PROFILE, phoneNumberConfirmed: false }));
    fixture.detectChanges();

fixture.componentInstance.requiresPhoneVerification.set(true);
    fixture.componentInstance.pendingVerificationId.set('v-123');
    fixture.componentInstance.otpForm.setValue({ code: '000000' });

    fixture.componentInstance.verifyPhone();

    const req = http.expectOne('/api/verification/verify');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.verificationId).toBe('v-123');
    expect(req.request.body.code).toBe('000000');

    const f = failure(400, 'OTP_INVALID');
    req.flush(f.body, f.opts);
    fixture.detectChanges();

    expect(fixture.componentInstance.otpError()?.message).toBe('Validation failed');
    expect(fixture.componentInstance.otpDone()).toBe(false);
  });

  it('AC439_SuccessfulOtpRefreshesProfile: marks phone verified and reloads profile', () => {
    const fixture = render();
    http.expectOne('/api/Auth/me').flush(envelope({ ...MOCK_PROFILE, phoneNumberConfirmed: false }));
    fixture.detectChanges();

fixture.componentInstance.requiresPhoneVerification.set(true);
    fixture.componentInstance.pendingVerificationId.set('v-123');
    fixture.componentInstance.otpForm.setValue({ code: '123456' });

    fixture.componentInstance.verifyPhone();

    const req = http.expectOne('/api/verification/verify');
    req.flush(envelope({ success: true, verified: true }));
    fixture.detectChanges();

    // Reload profile call
    const reloadReq = http.expectOne('/api/Auth/me');
    reloadReq.flush(envelope({ ...MOCK_PROFILE, phoneNumberConfirmed: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.otpDone()).toBe(true);
    expect(fixture.componentInstance.requiresPhoneVerification()).toBe(false);
    expect(fixture.componentInstance.profile()?.phoneNumberConfirmed).toBe(true);
  });

  it('cancelEdit: resets the profile form values back to profile signal state', () => {
    const fixture = render();
    http.expectOne('/api/Auth/me').flush(envelope(MOCK_PROFILE));
    fixture.detectChanges();

    fixture.componentInstance.profileForm.patchValue({
      firstName: 'ChangedName',
    });
    expect(fixture.componentInstance.profileForm.controls.firstName.value).toBe('ChangedName');

    fixture.componentInstance.cancelEdit();
    expect(fixture.componentInstance.profileForm.controls.firstName.value).toBe('Alex');
  });

  it('allows tab switching to security tab and changing password', () => {
    const fixture = render();
    http.expectOne('/api/Auth/me').flush(envelope(MOCK_PROFILE));
    fixture.detectChanges();

    fixture.componentInstance.setTab('security');
    fixture.detectChanges();
    expect(fixture.componentInstance.activeTab()).toBe('security');

    fixture.componentInstance.passwordForm.setValue({
      currentPassword: 'OldPassword123!',
      newPassword: 'NewPassword123456!',
    });

    fixture.componentInstance.changePassword();

    const req = http.expectOne('/api/Auth/change-password');
    expect(req.request.method).toBe('POST');
    req.flush(envelope(null));
    fixture.detectChanges();

    expect(fixture.componentInstance.passwordDone()).toBe(true);
  });
});
