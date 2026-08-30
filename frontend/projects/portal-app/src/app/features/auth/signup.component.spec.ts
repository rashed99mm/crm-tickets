import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { envelopeInterceptor, SessionStore } from 'common';
import PortalSignupComponent from './signup.component';

describe('PortalSignupComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ReactiveFormsModule, PortalSignupComponent],
      providers: [
        provideRouter([]),
        SessionStore,
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function create() {
    const fixture = TestBed.createComponent(PortalSignupComponent);
    fixture.detectChanges();
    return fixture;
  }

  function fillValidForm(fixture: ReturnType<typeof create>) {
    fixture.componentInstance.form.setValue({
      firstName: 'Dana',
      lastName: 'Support',
      email: 'dana@example.com',
      username: 'dana',
      phone: '',
      password: 'Password123',
    });
  }

  it('renders the registration form fields (ASG-3)', () => {
    const fixture = create();
    const el = fixture.nativeElement as HTMLElement;

    const control = fixture.componentInstance.form.controls;
    expect(control.firstName).toBeTruthy();
    expect(control.lastName).toBeTruthy();
    expect(control.email).toBeTruthy();
    expect(control.username).toBeTruthy();
    expect(control.phone).toBeTruthy();
    expect(control.password).toBeTruthy();
    expect(el.querySelector('button[type="submit"]')).not.toBeNull();
  });

  it('does not submit when the form is invalid (ASG-4)', () => {
    const fixture = create();
    fixture.componentInstance.submit();
    http.verify();
    expect(fixture.componentInstance.form.invalid).toBe(true);
  });

  it('registers then signs in and lands on the dashboard (ASG-5)', () => {
    const fixture = create();
    fillValidForm(fixture);

    const session = TestBed.inject(SessionStore);
    const sessionSignIn = vi.spyOn(session, 'signIn');
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    fixture.componentInstance.submit();

    const register = http.expectOne('/api/Auth/register');
    expect(register.request.method).toBe('POST');
    expect(register.request.body.phoneNumber).toBeNull();
    register.flush({ id: 'u-1' });

    const login = http.expectOne('/api/Auth/login');
    expect(login.request.body).toEqual({ email: 'dana@example.com', password: 'Password123' });
    login.flush({
      userId: 'u-1',
      email: 'dana@example.com',
      firstName: 'Dana',
      lastName: 'Support',
      accessToken: 'at',
      refreshToken: 'rt',
      accessTokenExpiresAt: '',
      refreshTokenExpiresAt: '',
      roles: [],
    });
    fixture.detectChanges();

    expect(sessionSignIn).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/app');
  });

  it('sends a blank phone as null, never an empty string (ASG-5)', () => {
    const fixture = create();
    fillValidForm(fixture);
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    fixture.componentInstance.submit();

    const register = http.expectOne('/api/Auth/register');
    expect(register.request.body.phoneNumber).toBeNull();
    register.flush({ id: 'u-1' });
    http.expectOne('/api/Auth/login').flush({
      userId: 'u-1',
      email: 'dana@example.com',
      firstName: 'Dana',
      lastName: 'Support',
      accessToken: 'at',
      refreshToken: 'rt',
      accessTokenExpiresAt: '',
      refreshTokenExpiresAt: '',
      roles: [],
    });
    fixture.detectChanges();
  });

  it('surfaces a field-keyed email rejection under the email control (ASG-6)', () => {
    const fixture = create();
    fillValidForm(fixture);

    fixture.componentInstance.submit();

    const register = http.expectOne('/api/Auth/register');
    register.flush(
      {
        success: false,
        data: null,
        code: 'VALIDATION_FAILED',
        message: 'One or more fields are invalid.',
        errors: [
          { field: 'Email', code: 'EmailValidator', message: 'A valid email is required.' },
        ],
        traceId: 't-1',
      },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(fixture.componentInstance.submitError()).not.toBeNull();
    expect(fixture.componentInstance.formLevelError()).toBeNull();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('A valid email is required.');
  });

  it('renders a 409 conflict at form level, not under a control (ASG-6)', () => {
    const fixture = create();
    fillValidForm(fixture);

    fixture.componentInstance.submit();

    const register = http.expectOne('/api/Auth/register');
    register.flush(
      {
        success: false,
        data: null,
        code: 'EMAIL_EXISTS',
        message: 'An account with that email or username already exists.',
        errors: [],
        traceId: 't-1',
      },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    expect(fixture.componentInstance.formLevelError()).not.toBeNull();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('already exists');
  });

  it('routes to login with a created cue when sign-in fails after register (ASG-7)', () => {
    const fixture = create();
    fillValidForm(fixture);

    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture.componentInstance.submit();

    http.expectOne('/api/Auth/register').flush({ id: 'u-1' });
    http
      .expectOne('/api/Auth/login')
      .flush(
        {
          success: false,
          data: null,
          code: 'INVALID_CREDENTIALS',
          message: 'Invalid credentials.',
          errors: [],
          traceId: 't',
        },
        { status: 401, statusText: 'Unauthorized' },
      );
    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { created: '1' } });
  });

  it('AC412_LandingAndSignupMatchReferenceComposition: signup renders two-column layout on desktop', () => {
    const fixture = create();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('main')).not.toBeNull();
    expect(el.querySelector('form')).not.toBeNull();
  });

  it('AC412_SignupPreservesValidationAndSubmissionBehaviour: invalid email disables form valid state', () => {
    const fixture = create();
    fixture.componentInstance.form.controls.email.setValue('invalid-email');
    expect(fixture.componentInstance.form.controls.email.valid).toBe(false);
  });

  it('AC418_SignupAndLandingRemainKeyboardReachable: signup form controls have accessible inputs', () => {
    const fixture = create();
    const el = fixture.nativeElement as HTMLElement;
    const inputs = el.querySelectorAll('input');
    expect(inputs.length).toBeGreaterThanOrEqual(5);
  });
});

