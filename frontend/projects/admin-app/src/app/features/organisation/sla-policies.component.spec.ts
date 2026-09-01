import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfirmationService, envelopeInterceptor } from 'common';
import SLAPoliciesComponent from './sla-policies.component';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

const POLICY = {
  id: 'p-1',
  priority: 'High',
  responseTargetHours: 2,
  resolutionTargetHours: 8,
  categoryId: null,
  branchId: null,
  isActive: true,
  createdAt: '2026-08-20T00:00:00Z',
};

describe('SLAPoliciesComponent', () => {
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

  function render(): ComponentFixture<SLAPoliciesComponent> {
    const fixture = TestBed.createComponent(SLAPoliciesComponent);
    fixture.detectChanges();
    http
      .expectOne((r) => r.url === '/api/SLAPolicies')
      .flush(ok({ items: [POLICY], pageIndex: 1, pageSize: 100, totalCount: 1 }));
    http
      .expectOne((r) => r.url === '/api/BusinessHours/calendars')
      .flush(ok({ items: [], pageIndex: 1, pageSize: 100, totalCount: 0 }));
    http
      .expectOne((r) => r.url === '/api/BusinessHours/holidays')
      .flush(ok({ items: [], pageIndex: 1, pageSize: 100, totalCount: 0 }));
    fixture.detectChanges();
    return fixture;
  }

  it('AC157: editing a policy calls PUT with the changed values and refreshes the list', () => {
    const fixture = render();

    fixture.componentInstance.startEdit(POLICY as never);
    fixture.detectChanges();
    fixture.componentInstance.editForm.controls.responseTargetHours.setValue(3);
    fixture.componentInstance.saveEdit();

    const request = http.expectOne((r) => r.url === '/api/SLAPolicies/p-1' && r.method === 'PUT');
    expect(request.request.body).toEqual({
      priority: 'High',
      responseTargetHours: 3,
      resolutionTargetHours: 8,
    });
    request.flush(ok(null));

    http
      .expectOne((r) => r.url === '/api/SLAPolicies')
      .flush(
        ok({
          items: [{ ...POLICY, responseTargetHours: 3 }],
          pageIndex: 1,
          pageSize: 100,
          totalCount: 1,
        }),
      );
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('3');
  });

  it('creates a business-hours calendar row and refreshes the list', () => {
    const fixture = render();

    fixture.componentInstance.calendarForm.setValue({
      branchId: '11111111-1111-1111-1111-111111111111',
      dayOfWeek: 'Monday',
      openTime: '09:00',
      closeTime: '17:00',
    });
    fixture.componentInstance.createBusinessHours();

    const request = http.expectOne((r) => r.url === '/api/BusinessHours/calendars' && r.method === 'POST');
    expect(request.request.body).toEqual({
      branchId: '11111111-1111-1111-1111-111111111111',
      dayOfWeek: 'Monday',
      openTime: '09:00',
      closeTime: '17:00',
    });
    request.flush(ok({ id: 'cal-1' }));

    http
      .expectOne((r) => r.url === '/api/BusinessHours/calendars')
      .flush(
        ok({
          items: [
            {
              id: 'cal-1',
              branchId: '11111111-1111-1111-1111-111111111111',
              dayOfWeek: 'Monday',
              openTime: '09:00',
              closeTime: '17:00',
            },
          ],
          pageIndex: 1,
          pageSize: 100,
          totalCount: 1,
        }),
      );
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Monday');
  });

  it('AC807_7_DeactivateConfirmsBeforeSending: cancelling issues no request', () => {
    const fixture = render();
    const confirmations = TestBed.inject(ConfirmationService);

    fixture.componentInstance.deactivate(POLICY as never);

    expect(confirmations.current()).not.toBeNull();
    expect(confirmations.current()?.danger).toBe(true);
    http.expectNone((r) => r.method === 'DELETE');

    confirmations.resolve(false);

    http.expectNone((r) => r.method === 'DELETE');
  });

  it('AC807_7_DeactivateSendsAfterConfirming', () => {
    const fixture = render();
    const confirmations = TestBed.inject(ConfirmationService);

    fixture.componentInstance.deactivate(POLICY as never);
    confirmations.resolve(true);

    const request = http.expectOne((r) => r.method === 'DELETE' && r.url === '/api/SLAPolicies/p-1');
    request.flush(ok(null));

    http.expectOne((r) => r.url === '/api/SLAPolicies');
  });
});
