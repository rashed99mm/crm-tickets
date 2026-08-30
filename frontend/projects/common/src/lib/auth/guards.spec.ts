import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { roleGuard } from './guards';
import { SessionStore } from './session.store';

function fakeSession(roles: readonly string[]): Pick<SessionStore, 'isAuthenticated' | 'hasRole'> {
  return {
    isAuthenticated: (() => true) as SessionStore['isAuthenticated'],
    hasRole: (role: string) => roles.includes(role),
  };
}

describe('roleGuard', () => {
  it('AC164: admits a caller holding ANY of several listed roles', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionStore, useValue: fakeSession(['Supervisor']) },
      ],
    });

    const result = TestBed.runInInjectionContext(() =>
      roleGuard('Supervisor', 'Admin')({} as never, { url: '/reports/ticket-volume' } as never),
    );

    expect(result).toBe(true);
  });

  it('AC164: refuses a caller holding none of the listed roles', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionStore, useValue: fakeSession(['Agent']) },
      ],
    });

    const result = TestBed.runInInjectionContext(() =>
      roleGuard('Supervisor', 'Admin')({} as never, { url: '/reports/ticket-volume' } as never),
    );

    expect(result).not.toBe(true);
  });

  it('a single-role call still behaves exactly as before this change', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionStore, useValue: fakeSession(['Admin']) },
      ],
    });

    const result = TestBed.runInInjectionContext(() =>
      roleGuard('Admin')({} as never, { url: '/departments' } as never),
    );

    expect(result).toBe(true);
  });
});
