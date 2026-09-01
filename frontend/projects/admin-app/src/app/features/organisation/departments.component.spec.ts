import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfirmationService, envelopeInterceptor } from 'common';
import DepartmentsComponent from './departments.component';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

const DEPARTMENT = {
  id: 'd-1',
  name: 'Support',
  managerId: null,
  isActive: true,
  createdAt: '2026-08-01T00:00:00Z',
};

describe('DepartmentsComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function render(): ComponentFixture<DepartmentsComponent> {
    const fixture = TestBed.createComponent(DepartmentsComponent);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/Departments').flush(ok({ items: [DEPARTMENT] }));
    fixture.detectChanges();
    return fixture;
  }

  it('AC807_6_DeactivateConfirmsBeforeSending: cancelling issues no request', () => {
    const fixture = render();
    const confirmations = TestBed.inject(ConfirmationService);

    fixture.componentInstance.deactivate(DEPARTMENT);

    expect(confirmations.current()).not.toBeNull();
    expect(confirmations.current()?.danger).toBe(true);
    http.expectNone((r) => r.method === 'DELETE');

    confirmations.resolve(false);

    http.expectNone((r) => r.method === 'DELETE');
  });

  it('AC807_6_DeactivateSendsAfterConfirming', () => {
    const fixture = render();
    const confirmations = TestBed.inject(ConfirmationService);

    fixture.componentInstance.deactivate(DEPARTMENT);
    confirmations.resolve(true);

    const request = http.expectOne((r) => r.method === 'DELETE' && r.url === '/api/Departments/d-1');
    request.flush(ok(null));

    http.expectOne((r) => r.url === '/api/Departments');
  });
});
