# Frontend Auth Management Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** The Angular staff-administration surface — list, create, activate/deactivate — reusing the shared auth/session foundation (`AUTH-18`..`AUTH-22`, `FE-2`). This is the client half of `EPIC-09-US-112-auth-management`.

**Architecture:** `users.component.ts` (admin-app) → `StaffApi` (common). List is an `AsyncState` union; create posts `roles: [role]` with the platform's real two-role vocabulary (`Admin`/`User`). The route is `roleGuard('Admin')` — a courtesy; the `Admin` policy on `/api/Users` is the control.

## Global constraints

- The signed-in user cannot deactivate themselves (`AUTH-13`) — `ownUserId` from `SessionStore` disables the toggle.
- Server field errors bind to the right input via `fieldError(field)` (`AUTH-19`).
- No hand-built envelope handling in the component — failures arrive as `ApiError` from the envelope interceptor.

## Task 1 — `StaffApi` (`AUTH-` surface)

**Files:** `frontend/projects/common/src/lib/auth/staff.api.ts`

**Interfaces:** `StaffApi.list()`, `create(CreateStaffRequest)`, `setActive(id, isActive)`, `changeOwnPassword(...)`.

**Step 1 — Real service**

```ts
@Injectable({ providedIn: 'root' })
export class StaffApi {
  private readonly http = inject(HttpClient);
  list(): Observable<PagedResult<StaffUser>> { return this.http.get<PagedResult<StaffUser>>('/api/Users'); }
  create(request: CreateStaffRequest): Observable<unknown> { return this.http.post('/api/Users', request); }
  setActive(id: string, isActive: boolean): Observable<unknown> {
    const action = isActive ? 'activate' : 'deactivate';
    return this.http.put(`/api/Users/${id}/${action}`, {});
  }
  changeOwnPassword(currentPassword: string, newPassword: string): Observable<unknown> {
    return this.http.post('/api/Auth/change-password', { currentPassword, newPassword });
  }
}
export interface StaffUser {
  readonly id: string; readonly email: string; readonly username: string;
  readonly firstName: string; readonly lastName: string;
  readonly isActive: boolean; readonly createdAt: string; readonly roles: readonly string[];
}
```

- [ ] **Step 2: Run:** `cd frontend && npx ng test common --watch=false --filter staff.api`
Expected: PASS — `list` → `/api/Users`; `setActive(false)` → `PUT /api/Users/{id}/deactivate`.

- [ ] **Step 3: Commit:** `git add frontend/projects/common/src/lib/auth/staff.api.ts && git commit -m "feat(auth-fe): StaffApi (AUTH surface)"`

## Task 2 — Users component (`AUTH-18`..`AUTH-22`)

**Files:** `frontend/projects/admin-app/src/app/features/users/users.component.ts`

**Step 1 — Real component (excerpt)**

```ts
export default class UsersComponent {
  readonly state = signal<AsyncState<readonly StaffUser[]>>(loading());
  readonly saving = signal(false);
  readonly createError = signal<ApiError | null>(null);
  readonly showCreate = signal(false);
  readonly ownUserId = computed(() => this.session.userId());   // cannot deactivate self (AUTH-13)

  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    username: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    role: new FormControl('User', { nonNullable: true, validators: [Validators.required] }),  // real vocab
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] }),
  });

  load(): void {
    this.state.set(loading());
    this.api.list().subscribe({
      next: (result) => this.state.set(fromList(result.items)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }
  create(): void {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true); this.createError.set(null);
    const { email, username, firstName, lastName, role, password } = this.form.getRawValue();
    this.api.create({ email, username, firstName, lastName, password, roles: [role] }).subscribe({
      next: () => { this.saving.set(false); this.form.reset({ role: 'User' }); this.showCreate.set(false); this.load(); },
      error: (error: unknown) => this.createError.set(this.toApiError(error)),
    });
  }
  toggleActive(user: StaffUser): void {
    this.api.setActive(user.id, !user.isActive).subscribe({ next: () => this.load(), error: (e) => this.state.set(failed(this.toApiError(e))) });
  }
  fieldError(field: string) { return this.createError()?.fieldError(field) ?? null; }   // AUTH-19
}
```

`fromList` only sees a success payload, so an error can never be collapsed into "no staff" (`AUTH-18`). The `role` control defaults to `"User"` and is sent as `roles: [role]` — matching the platform's `Admin`/`User` vocabulary, not the old `Supervisor`/`Agent` naming.

- [ ] **Step 2: Run:** `cd frontend && npx ng test admin-app --watch=false --filter users`
Expected: PASS — list/empty/error distinct; server field error binds to control; self-deactivate disabled.

- [ ] **Step 3: Commit:** `git add frontend/projects/admin-app/src/app/features/users/ && git commit -m "feat(auth-fe): users list/create/activate (AUTH-18..AUTH-22)"`

## Task 3 — Route guard

**Files:** `frontend/projects/admin-app/src/app/app.routes.ts` — `users` route carries `canActivate: [roleGuard('Admin')]`.

- [ ] **Step 1: Run:** `cd frontend && npx ng build admin-app`
Expected: clean build; `/users` guarded.

- [ ] **Step 2: Commit:** `git add frontend/projects/admin-app/src/app/app.routes.ts && git commit -m "feat(auth-fe): users route roleGuard('Admin')"`

## Self-review

Coverage: `AUTH-18`..`AUTH-22` → Tasks 1,2; route → Task 3.

**Discrepancy found:** the old plan assumed a `Supervisor` policy and `Supervisor`/`Agent` roles. The shipped code uses `Admin` policy at `/api/Users` and `Admin`/`User` roles on the client (`users.component.ts` comment confirms). The rewrite reflects the real vocabulary.
