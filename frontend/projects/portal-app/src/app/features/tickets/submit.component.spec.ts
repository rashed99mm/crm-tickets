import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { envelopeInterceptor, PortalApi } from 'common';
import PortalSubmitComponent from './submit.component';

describe('PortalSubmitComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ReactiveFormsModule, PortalSubmitComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function create() {
    const fixture = TestBed.createComponent(PortalSubmitComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders no customer select, but a description field and a submit button', () => {
    const fixture = create();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('select#customer')).toBeNull();
    expect(el.querySelector('textarea#description')).not.toBeNull();
    http.expectOne((r) => r.url.includes('/api/Categories')).flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: [],
      errors: [],
    });
  });

  it('does not submit when the form is invalid', () => {
    const fixture = create();
    http.expectOne((r) => r.url.includes('/api/Categories')).flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: [],
      errors: [],
    });
    fixture.componentInstance.submit();
    http.verify();
  });

  it('posts to /api/portal/tickets with no customerId when valid', () => {
    const fixture = create();
    http.expectOne((r) => r.url.includes('/api/Categories')).flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: [{ id: 'c1', name: 'Billing' }],
      errors: [],
    });

    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    fixture.componentInstance.form.setValue({
      subject: 'Cannot log in',
      categoryId: 'c1',
      description: 'Help',
    });
    fixture.componentInstance.submit();

    const req = http.expectOne('/api/portal/tickets');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      subject: 'Cannot log in',
      categoryId: 'c1',
      description: 'Help',
    });
    expect(req.request.body).not.toHaveProperty('customerId');
    // US-923 / spec A2 — the customer does not self-classify; the server derives priority.
    expect(req.request.body).not.toHaveProperty('priority');
    req.flush({ success: true, code: 'CON032', message: 'OK', data: { id: 't1' }, errors: [] });
    fixture.detectChanges();
  });
});
