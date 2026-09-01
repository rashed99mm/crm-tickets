import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ConfirmationService, envelopeInterceptor, LocaleStore } from 'common';
import PermissionsComponent from './permissions.component';

const MODEL = {
  roles: [{ id: 'role-1', name: 'Admin', permissionIds: ['permission-1'] }],
  permissions: [
    { id: 'permission-1', name: 'ticket.view', description: 'View tickets' },
    { id: 'permission-2', name: 'ticket.close', description: 'Close tickets' },
  ],
};

/**
 * Two roles, three permissions spanning two resource groups (`ticket.*`, `report.*`).
 * Admin holds `permission-1` (ticket.view); Agent holds `permission-2` (ticket.close).
 * Column order follows this array, so `tbody input[type="checkbox"]` order is:
 *   [0] Admin×ticket.view (checked)   [1] Admin×ticket.close   [2] Admin×report.view
 *   [3] Agent×ticket.view             [4] Agent×ticket.close (checked)  [5] Agent×report.view
 */
const MODEL_TWO_ROLES = {
  roles: [
    { id: 'role-1', name: 'Admin', permissionIds: ['permission-1'] },
    { id: 'role-2', name: 'Agent', permissionIds: ['permission-2'] },
  ],
  permissions: [
    { id: 'permission-1', name: 'ticket.view', description: 'View tickets' },
    { id: 'permission-2', name: 'ticket.close', description: 'Close tickets' },
    { id: 'permission-3', name: 'report.view', description: 'View reports' },
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

  function flushList(fixture: ComponentFixture<PermissionsComponent>, data: unknown = MODEL) {
    http.expectOne('/api/admin/permissions').flush(envelope(data));
    fixture.detectChanges();
  }

  function text(fixture: ComponentFixture<PermissionsComponent>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function checkboxes(fixture: ComponentFixture<PermissionsComponent>): HTMLInputElement[] {
    return Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('tbody input[type="checkbox"]'),
    );
  }

  function searchInput(fixture: ComponentFixture<PermissionsComponent>): HTMLInputElement {
    return (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('[data-testid="permissions-search"]')!;
  }

  function type(fixture: ComponentFixture<PermissionsComponent>, value: string): void {
    const input = searchInput(fixture);
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function saveWith(fixture: ComponentFixture<PermissionsComponent>): void {
    fixture.componentInstance.save();
    fixture.detectChanges();
    TestBed.inject(ConfirmationService).resolve(true);
    fixture.detectChanges();
  }

  it('AC805_1_PermissionListRenders: shows loading and then the matrix with checked mappings', () => {
    const fixture = render();
    expect(text(fixture)).toContain('Loading');

    flushList(fixture);

    expect(text(fixture)).not.toContain('Loading');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr').length).toBe(1);
    expect(text(fixture)).toContain('Admin');
    expect(text(fixture)).toContain('ticket.view');
    expect(checkboxes(fixture).filter((box) => box.checked).length).toBe(1);
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

  // Replaces AC805_2_AssignPermissionToRole — the single-mapping endpoint it asserted is still
  // covered by Integration/PermissionTests.cs; what changed is that the screen no longer calls it
  // per click (spec Finding 4).
  it('AC806_11_TogglingStagesWithoutSendingAnything', () => {
    const fixture = render();
    flushList(fixture);

    const box = checkboxes(fixture)[1];
    expect(box.checked).toBe(false);
    box.click();
    fixture.detectChanges();

    http.expectNone(() => true);
    expect(checkboxes(fixture)[1].checked).toBe(true);
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="staged-marker"]')).not.toBeNull();
    expect(text(fixture)).toContain('1 unsaved changes');
  });

  it('AC806_24_TheActionBarIsAbsentUntilSomethingIsStaged', () => {
    const fixture = render();
    flushList(fixture);
    const bar = () => (fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-action-bar"]');
    expect(bar()).toBeNull();

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    expect(bar()).not.toBeNull();

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    expect(bar()).toBeNull();
  });

  it('AC806_24_SaveIsANoOpWhenNothingIsStaged', () => {
    const fixture = render();
    flushList(fixture);

    fixture.componentInstance.save();
    fixture.detectChanges();

    expect(TestBed.inject(ConfirmationService).current()).toBeNull();
    http.expectNone(() => true);
  });

  it('AC806_12_SaveConfirmsAndListsEveryStagedChange', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    const boxes = checkboxes(fixture);
    boxes[1].click(); // Admin × ticket.close → grant
    fixture.detectChanges();
    checkboxes(fixture)[4].click(); // Agent × ticket.close → revoke
    fixture.detectChanges();

    fixture.componentInstance.save();
    fixture.detectChanges();

    const request = TestBed.inject(ConfirmationService).current();
    expect(request).not.toBeNull();
    expect(request!.details).toEqual(['Grant ticket.close → Admin', 'Revoke ticket.close → Agent']);
    expect(request!.danger).toBe(true);
    http.expectNone(() => true);
  });

  it('AC806_14_CancellingTheDialogSendsNothingAndKeepsTheDraft', () => {
    const fixture = render();
    flushList(fixture);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    fixture.componentInstance.save();
    fixture.detectChanges();

    TestBed.inject(ConfirmationService).resolve(false);
    fixture.detectChanges();

    http.expectNone(() => true);
    expect(checkboxes(fixture)[1].checked).toBe(true);
    expect(text(fixture)).toContain('1 unsaved changes');
  });

  // Replaces AC805_3_RevokePermissionFromRole.
  it('AC806_13_AcceptingSendsOnePutPerDirtyRoleThenReloads', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click(); // Admin gains ticket.close
    fixture.detectChanges();
    checkboxes(fixture)[3].click(); // Agent gains ticket.view
    fixture.detectChanges();
    saveWith(fixture);

    const first = http.expectOne('/api/admin/permissions/role-1');
    expect(first.request.method).toBe('PUT');
    expect(first.request.body).toEqual({
      permissionIds: ['permission-1', 'permission-2'],
      expectedPermissionIds: ['permission-1'],
    });
    first.flush(envelope(null));
    fixture.detectChanges();

    const second = http.expectOne('/api/admin/permissions/role-2');
    expect(second.request.method).toBe('PUT');
    second.flush(envelope(null));
    fixture.detectChanges();

    flushList(fixture, MODEL_TWO_ROLES);
    // ToastService's success toast renders via CsToastHost in the app shell, not in this
    // component's own template — the observable state is what this test can see.
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-action-bar"]')).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-save-outcome"]')).toBeNull();
  });

  it('AC806_15_PartialFailureNamesWhatSavedAndWhatDidNot', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click(); // role-1 grant
    fixture.detectChanges();
    checkboxes(fixture)[5].click(); // role-2 grant
    fixture.detectChanges();
    saveWith(fixture);

    http.expectOne('/api/admin/permissions/role-1').flush(envelope(null));
    fixture.detectChanges();
    http.expectOne('/api/admin/permissions/role-2').flush(
      failure('ERR002', 'The last required permission cannot be removed from a built-in role.'),
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    flushList(fixture, MODEL_TWO_ROLES);

    const banner = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-save-outcome"]')!;
    expect(banner).not.toBeNull();
    expect(banner.textContent).toContain('1 of 2 roles saved.');
    expect(banner.textContent).toContain('Agent');
    expect(banner.textContent).toContain('A built-in role must keep at least one permission.');

    expect(checkboxes(fixture)[5].checked).toBe(true);
    expect(text(fixture)).toContain('1 unsaved changes');
  });

  it('AC806_17_BuiltInRoleRefusalKeepsTheStagedChange', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[0].click(); // revoke role-1's only permission
    fixture.detectChanges();
    saveWith(fixture);

    http.expectOne('/api/admin/permissions/role-1').flush(
      failure('ERR002', 'The last required permission cannot be removed from a built-in role.'),
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();
    flushList(fixture, MODEL_TWO_ROLES);

    const body = text(fixture);
    expect(body).toContain('A built-in role must keep at least one permission.');
    expect(body).not.toContain('Permission changes saved.');
    expect(checkboxes(fixture)[0].checked).toBe(false);
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-reload"]')).toBeNull();
  });

  it('AC806_16_StaleRefusalOffersAReloadThatDropsThatRolesDraft', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    saveWith(fixture);

    http.expectOne('/api/admin/permissions/role-1').flush(
      failure('ERR087', "This role's permissions were changed by someone else."),
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();
    flushList(fixture, MODEL_TWO_ROLES);

    expect(text(fixture)).toContain('Someone else changed this role');
    expect(checkboxes(fixture)[1].checked).toBe(true);

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-reload"] button')!
      .click();
    fixture.detectChanges();

    flushList(fixture, MODEL_TWO_ROLES);
    expect(checkboxes(fixture)[1].checked).toBe(false);
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-action-bar"]')).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-save-outcome"]')).toBeNull();
  });

  it('AC806_18_DiscardConfirmsAndCancellingKeepsTheDraft', () => {
    const fixture = render();
    flushList(fixture);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-discard"] button')!
      .click();
    fixture.detectChanges();

    const confirmations = TestBed.inject(ConfirmationService);
    expect(confirmations.current()?.title).toContain('Discard');

    confirmations.resolve(false);
    fixture.detectChanges();

    expect(checkboxes(fixture)[1].checked).toBe(true);
    http.expectNone(() => true);
  });

  it('AC806_18_DiscardAcceptedResetsToTheServerState', () => {
    const fixture = render();
    flushList(fixture);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    fixture.componentInstance.discard();
    fixture.detectChanges();
    TestBed.inject(ConfirmationService).resolve(true);
    fixture.detectChanges();

    expect(checkboxes(fixture)[1].checked).toBe(false);
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-action-bar"]')).toBeNull();
    http.expectNone(() => true);
  });

  it('AC806_20_RefreshConfirmsOnlyWhenDirty', () => {
    const fixture = render();
    flushList(fixture);

    fixture.componentInstance.refresh();
    fixture.detectChanges();
    expect(TestBed.inject(ConfirmationService).current()).toBeNull();
    flushList(fixture);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    fixture.componentInstance.refresh();
    fixture.detectChanges();

    const confirmations = TestBed.inject(ConfirmationService);
    expect(confirmations.current()).not.toBeNull();
    confirmations.resolve(false);
    fixture.detectChanges();

    http.expectNone(() => true);
    expect(checkboxes(fixture)[1].checked).toBe(true);
  });

  it('AC806_19_ConfirmLeaveAsksWhenDirty', () => {
    const fixture = render();
    flushList(fixture);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();

    let allowed: boolean | null = null;
    fixture.componentInstance.confirmLeave().subscribe((value) => (allowed = value));
    fixture.detectChanges();

    const confirmations = TestBed.inject(ConfirmationService);
    expect(confirmations.current()?.danger).toBe(true);
    confirmations.resolve(true);

    expect(allowed).toBe(true);
  });

  it('AC806_21_SearchNarrowsTheColumns', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);
    expect(checkboxes(fixture).length).toBe(6);

    type(fixture, 'report');

    expect(checkboxes(fixture).length).toBe(2);
    expect(text(fixture)).toContain('report.view');
    expect(text(fixture)).not.toContain('ticket.close');
  });

  it('AC806_21_SearchMatchesTheDescriptionToo', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    type(fixture, 'Close tickets');

    expect(checkboxes(fixture).length).toBe(2);
    expect(text(fixture)).toContain('ticket.close');
  });

  it('AC806_21_NoMatchShowsAnInTableMessageNotThePageLevelEmptyState', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    type(fixture, 'zzzz');

    const body = text(fixture);
    expect(body).toContain('No permission matches this search.');
    expect(body).not.toContain('No permissions found');
    expect(checkboxes(fixture).length).toBe(0);

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-clear-search"] button')!
      .click();
    fixture.detectChanges();

    expect(checkboxes(fixture).length).toBe(6);
  });

  it('AC806_21_SearchDoesNotDiscardStagedChanges', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    type(fixture, 'report');
    expect(text(fixture)).toContain('1 unsaved changes');

    type(fixture, '');
    expect(checkboxes(fixture)[1].checked).toBe(true);
  });

  it('AC806_22_GroupsRenderWithCountsAndCollapse', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    const groupHeaders = (fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid^="permissions-group-"]');
    expect(groupHeaders.length).toBe(2);
    expect(text(fixture)).toContain('Tickets');
    expect(text(fixture)).toContain('Reports');

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-group-ticket"] button')!
      .click();
    fixture.detectChanges();

    expect(checkboxes(fixture).length).toBe(2); // only report.view remains interactive
    const summaries = (fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="permissions-group-summary"]');
    expect(summaries.length).toBe(2);
    expect(summaries[0].textContent).toContain('1/2');
  });

  it('AC806_22_CollapsingAGroupKeepsItsStagedChanges', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    expect(text(fixture)).toContain('1 unsaved changes');

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-group-ticket"] button')!
      .click();
    fixture.detectChanges();

    expect(text(fixture)).toContain('1 unsaved changes');
    expect(
      (fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="permissions-group-summary"]')[0]
        .textContent,
    ).toContain('2/2');
  });

  it('AC806_23_GrantAllStagesEveryVisiblePermissionWithoutSending', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-grant-all-role-1"] button')!
      .click();
    fixture.detectChanges();

    http.expectNone(() => true);
    expect(text(fixture)).toContain('2 unsaved changes');
    expect(checkboxes(fixture).slice(0, 3).every((box) => box.checked)).toBe(true);
  });

  it('AC806_23_BulkActionsRespectTheSearchFilter', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    type(fixture, 'report');
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-grant-all-role-1"] button')!
      .click();
    fixture.detectChanges();

    expect(text(fixture)).toContain('1 unsaved changes');

    type(fixture, '');
    expect(checkboxes(fixture)[1].checked).toBe(false);
  });

  it('AC806_23_RevokeAllStagesRemovalOfVisiblePermissions', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="permissions-revoke-all-role-1"] button')!
      .click();
    fixture.detectChanges();

    http.expectNone(() => true);
    expect(text(fixture)).toContain('1 unsaved changes');
    expect(checkboxes(fixture).slice(0, 3).some((box) => box.checked)).toBe(false);
  });

  it('AC806_25_TheStagedMarkerNamesTheDirectionAndIsNotColourOnly', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    checkboxes(fixture)[1].click(); // grant
    fixture.detectChanges();
    expect(text(fixture)).toContain('staged: grant');

    checkboxes(fixture)[0].click(); // revoke
    fixture.detectChanges();
    expect(text(fixture)).toContain('staged: revoke');

    const marker = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="staged-marker"]')!;
    expect(marker.textContent?.trim().length).toBeGreaterThan(0);
  });

  it('AC806_25_TheLiveRegionSurvivesTheCountReachingZero', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    const region = () => (fixture.nativeElement as HTMLElement).querySelector('[data-testid="permissions-announcer"]');

    expect(region()).not.toBeNull();
    expect(region()!.getAttribute('aria-live')).toBe('polite');
    expect(region()!.textContent).toContain('No unsaved permission changes.');

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    expect(region()!.textContent).toContain('1 unsaved changes');

    checkboxes(fixture)[1].click();
    fixture.detectChanges();
    expect(region()).not.toBeNull();
    expect(region()!.textContent).toContain('No unsaved permission changes.');
  });

  it('AC806_25_EveryInteractiveControlIsKeyboardReachable', () => {
    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);
    checkboxes(fixture)[1].click();
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const focusable = host.querySelectorAll<HTMLElement>('button, input, [tabindex]:not([tabindex="-1"])');

    for (const element of Array.from(focusable)) {
      expect(element.getAttribute('tabindex')).not.toBe('-1');
    }
    for (const box of checkboxes(fixture)) {
      expect(box.getAttribute('aria-label')?.length).toBeGreaterThan(0);
    }
    const groupToggle = host.querySelector('[data-testid="permissions-group-ticket"] button')!;
    expect(groupToggle.getAttribute('aria-expanded')).toBe('true');
  });

  it('AC806_26_RendersUnderArabicRtl', () => {
    const locale = TestBed.inject(LocaleStore);
    locale.setLocale('ar');

    const fixture = render();
    flushList(fixture, MODEL_TWO_ROLES);

    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    const body = text(fixture);
    expect(body).toContain('إدارة الصلاحيات');
    expect(body).toContain('التذاكر');
    expect(body).not.toContain('permissions.');

    locale.setLocale('en');
  });
});
