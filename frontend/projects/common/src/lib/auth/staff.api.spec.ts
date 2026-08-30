import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StaffApi, StaffProfile, UpdateProfileRequest, VerifyOtpRequest } from './staff.api';

describe('StaffApi', () => {
  let api: StaffApi;
  let http: HttpTestingController;

  const mockProfile: StaffProfile = {
    id: 'user-123',
    email: 'alex.morgan@commandcenter.crm',
    username: 'alex.morgan',
    firstName: 'Alex',
    lastName: 'Morgan',
    phoneNumber: '+14155550100',
    emailConfirmed: true,
    phoneNumberConfirmed: false,
    isActive: true,
    createdAt: '2026-08-25T10:00:00Z',
    roles: ['Agent'],
    profileImageUrl: 'https://example.com/avatar.png',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(StaffApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('AC430_GetCurrentProfile: fetches current user profile via GET /api/Auth/me', () => {
    let received: StaffProfile | undefined;
    api.getCurrentProfile().subscribe((res) => (received = res));

    const req = http.expectOne('/api/Auth/me');
    expect(req.request.method).toBe('GET');
    req.flush(mockProfile);

    expect(received).toEqual(mockProfile);
  });

  it('AC430_UpdateCurrentProfile: sends PUT /api/Auth/me with only allowed profile fields', () => {
    const updateRequest: UpdateProfileRequest = {
      firstName: 'Alex',
      lastName: 'Morgan',
      phoneNumber: '+14155550100',
      profileImageUrl: 'https://example.com/avatar.png',
    };

    let received: StaffProfile | undefined;
    api.updateCurrentProfile(updateRequest).subscribe((res) => (received = res));

    const req = http.expectOne('/api/Auth/me');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(updateRequest);
    req.flush({ ...mockProfile, firstName: 'Alex' });

    expect(received?.firstName).toBe('Alex');
  });

  it('AC439_VerifyOtp: posts code and verificationId to /api/verification/verify', () => {
    const verifyRequest: VerifyOtpRequest = {
      verificationId: 'ver-123',
      code: '123456',
    };

    let response: unknown;
    api.verifyOtp(verifyRequest).subscribe((res) => (response = res));

    const req = http.expectOne('/api/verification/verify');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(verifyRequest);
    req.flush({ success: true, verified: true });

    expect(response).toEqual({ success: true, verified: true });
  });

  it('AC436_RequestPhoneVerification: posts phoneNumber to /api/verification/request-phone', () => {
    api.requestPhoneVerification('+14155550100').subscribe();

    const req = http.expectOne('/api/verification/request-phone');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ phoneNumber: '+14155550100' });
    req.flush({ success: true });
  });
});
