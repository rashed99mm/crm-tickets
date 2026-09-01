import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { ConfirmationService } from 'common';
import { Observable, of } from 'rxjs';
import { permissionsDirtyGuard, UnsavedChangesHost } from './permissions-dirty.guard';

describe('permissionsDirtyGuard', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [ConfirmationService] }));

  function run(host: UnsavedChangesHost): boolean | Observable<boolean> {
    const snapshot = {} as ActivatedRouteSnapshot;
    const state = {} as RouterStateSnapshot;
    return TestBed.runInInjectionContext(
      () => permissionsDirtyGuard(host, snapshot, state, state) as boolean | Observable<boolean>,
    );
  }

  it('AC806_19_LeavingACleanScreenIsNotInterrupted', () => {
    const host: UnsavedChangesHost = {
      hasUnsavedChanges: () => false,
      confirmLeave: () => of(true),
    };

    expect(run(host)).toBe(true);
    expect(TestBed.inject(ConfirmationService).current()).toBeNull();
  });

  it('AC806_19_LeavingADirtyScreenAsksAndRespectsNo', () => {
    let asked = 0;
    const host: UnsavedChangesHost = {
      hasUnsavedChanges: () => true,
      confirmLeave: () => {
        asked += 1;
        return of(false);
      },
    };

    const result = run(host) as Observable<boolean>;
    let allowed: boolean | null = null;
    result.subscribe((value) => (allowed = value));

    expect(asked).toBe(1);
    expect(allowed).toBe(false);
  });

  it('AC806_19_LeavingADirtyScreenRespectsYes', () => {
    const host: UnsavedChangesHost = {
      hasUnsavedChanges: () => true,
      confirmLeave: () => of(true),
    };

    let allowed: boolean | null = null;
    (run(host) as Observable<boolean>).subscribe((value) => (allowed = value));

    expect(allowed).toBe(true);
  });
});
