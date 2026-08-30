import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import PermissionsComponent from './permissions.component';

const MODEL = {
  roles: [{ id: 'role-1', name: 'Admin', permissionIds: ['permission-1'] }],
  permissions: [
    { id: 'permission-1', name: 'ticket.view', description: 'View tickets' },
    { id: 'permission-2', name: 'ticket.close', description: 'Close tickets' },
  ],
};

function envelope(data: unknown) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

function failure(code: string, message: string) {
  return { success: false, code, message, data: null, errors: [] };
}

describe('PermissionsComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function render(): ComponentFixture<PermissionsComponent> {
    const fixture = TestBed.createComponent(PermissionsComponent);
    fixture.detectChanges();
    return fixture;
  }

  function flushList(fixture: ComponentFixture<PermissionsComponent>, data = MODEL) {
    http.expectOne('/api/admin/permissions').flush(envelope(data));
    fixture.detectChanges();
  }

  function text(fixture: ComponentFixture<PermissionsComponent>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('AC805_1_PermissionListRenders: shows loading and then the matrix with checked mappings', () => {
    const fixture = render();
    expect(text(fixture)).toContain('Loading');

    flushList(fixture);

    expect(text(fixture)).not.toContain('Loading');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr').length).toBe(1);
    expect(text(fixture)).toContain('Admin');
    expect(text(fixture)).toContain('ticket.view');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('input[type="checkbox"]:checked').length).toBe(1);
  });

  it('AC805_1_PermissionListShowsVisibleError: shows a sad state with retry, not an empty state', () => {
    const fixture = render();
    http.expectOne('/api/admin/permissions').flush(
      failure('ERR005', 'Failed to load'),
      { status: 500, statusText: 'Server Error' },
    );
    fixture.detectChanges();

    const body = text(fixture);
    expect(body).toContain('Try again');
    expect(body).toContain('Failed to load');
    expect(body).not.toContain('No permissions found');
  });

  it('AC805_2_AssignPermissionToRole: posts the new assignment and reloads from the server', () => {
    const fixture = render();
    flushList(fixture);

    const checkbox = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('input[type="checkbox"]')[1];
    expect(checkbox.checked).toBe(false);
    checkbox.click();

    const mutation = http.expectOne('/api/admin/permissions/role-1/permission-2');
    expect(mutation.request.method).toBe('POST');
    mutation.flush(envelope(null));
    fixture.detectChanges();
    expect(text(fixture)).toContain('Permission assigned successfully.');

    // No optimistic local change — the checked state must follow the server reload.
    flushList(fixture);
  });

  it('AC805_3_RevokePermissionFromRole: deletes the assignment and reloads from the server', () => {
    const fixture = render();
    flushList(fixture);

    const checkbox = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('input[type="checkbox"]')[0];
    expect(checkbox.checked).toBe(true);
    checkbox.click();

    const mutation = http.expectOne('/api/admin/permissions/role-1/permission-1');
    expect(mutation.request.method).toBe('DELETE');
    mutation.flush(envelope(null));
    fixture.detectChanges();
    expect(text(fixture)).toContain('Permission revoked successfully.');

    flushList(fixture);
  });

  it('AC805_4_CannotRemoveLastPermission: surfaces a dedicated warning, no success, state retained', () => {
    const fixture = render();
    flushList(fixture);

    const checkbox = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('input[type="checkbox"]')[0];
    checkbox.click();

    const mutation = http.expectOne('/api/admin/permissions/role-1/permission-1');
    mutation.flush(failure('ERR002', 'The last required permission cannot be removed from a built-in role.'), {
      status: 409,
      statusText: 'Conflict',
    });
    fixture.detectChanges();

    const body = text(fixture);
    expect(body).toContain('A built-in role must keep at least one permission.');
    expect(body).not.toContain('Permission revoked successfully.');

    // On a mutation error the component must not optimistically alter the model and must not
    // reload; the cached role->permission mapping is therefore retained as-is. Asserting no
    // follow-up GET proves the mapping was not dropped and no success reload ran.
    http.expectNone('/api/admin/permissions');
    fixture.detectChanges();
    expect(fixture.componentInstance.isAssigned(MODEL.roles[0], 'permission-1')).toBe(true);
  });
});
