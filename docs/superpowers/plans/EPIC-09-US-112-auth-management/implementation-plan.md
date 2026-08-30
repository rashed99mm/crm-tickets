# Auth Management Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** Supervisors/Admins manage staff accounts (list, create, activate/deactivate), and any signed-in user changes their own password — end to end (`AUTH-1`..`AUTH-22`).

**Spec:** `docs/superpowers/specs/EPIC-09-US-112-auth-management-design.md`.

**Approach (as shipped):** the backend is the *inherited* Users surface from the adopted platform (`UsersController`, `Users/*` handlers), not the hand-built `AdminApi/Endpoints/UserEndpoints.cs` the old prose named. The Angular side is `users.component.ts` + `staff.api.ts` + the `SessionStore`/`roleGuard` foundation. Tests are the real `*.spec.ts` files plus the inherited integration suite.

## Global constraints

- Dependency rule holds: user management logic lives in `Application/Features/Users`; `UserManager`/`RoleManager` only in `Infrastructure`.
- Every new message key needs `DomainKey` + `SystemCode` + `SystemCodeMap` + `Resources.yaml`, or the `EveryErrorCode` guard fails the build.
- **Discrepancy (prose vs real code):** the old plan said the four staff routes were `Supervisor`-policy and lived at lowercase `/api/users`, `/api/auth/change-password` with a `Supervisor` policy. The real `UsersController` is `[Authorize(Policy = "Admin")]` and the route is `/api/Users` (capital, ASP.NET conventional); `change-password` is on `AuthController` under `/api/Auth`. The Users UI comment confirms the platform's actual vocabulary is **Admin / User**, not the old `Supervisor`/`Agent` naming the prose assumed.

## Task 1 — Real `UsersController` endpoints (`AUTH-1`..`AUTH-17`)

**Files:**
- `backend/src/CustomerSupport.InternalApi/Controllers/UsersController.cs`
- `backend/src/CustomerSupport.Application/Features/Users/Commands/{CreateUser,ActivateUser,DeactivateUser,AssignRoles,UpdateUser,DeleteUser}/*`
- `backend/src/CustomerSupport.Application/Features/Users/Queries/{GetUsers,GetUserById}/*`

**Interfaces:** `CreateUserCommand`, `ActivateUserCommand`, `DeactivateUserCommand`, `GetUsersQuery` → `PaginatedList<UserListItemDto>`.

**Step 1 — Real controller (excerpt)**

```csharp
// backend/src/CustomerSupport.InternalApi/Controllers/UsersController.cs
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Admin")]          // real policy: Admin, not Supervisor
public class UsersController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] bool? isActive = null, CancellationToken ct = default)
        => this.ToActionResult(await _mediator.Send(new GetUsersQuery
            { PageIndex = page, PageSize = pageSize, Search = search, IsActive = isActive }, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
        => this.ToActionResult(await _mediator.Send(new CreateUserCommand(
            request.Email, request.Username, request.Password, request.FirstName,
            request.LastName, request.PhoneNumber, request.Roles), ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        => this.ToActionResult(await _mediator.Send(new ActivateUserCommand(id), ct));

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => this.ToActionResult(await _mediator.Send(new DeactivateUserCommand(id), ct));
}
```

Activate/deactivate reuse `LockoutEnd` semantics in the inherited `IdentityUserService` (set/clear lockout) — the old prose's "deactivation sets `LockoutEnd = DateTimeOffset.MaxValue`" matches the shipped behaviour.

- [ ] **Step 2: Run — integration tests for the six endpoints**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~UsersEndpointTests"`
Expected: PASS covering `AUTH-1`..`AUTH-17`.

- [ ] **Step 3: Commit**

```bash
git add backend/src/CustomerSupport.InternalApi/Controllers/UsersController.cs \
        backend/src/CustomerSupport.Application/Features/Users/
git commit -m "feat(users): admin user management endpoints (AUTH-1..AUTH-17)"
```

## Task 2 — Change own password (`AUTH-` self-service)

**Files:** `backend/src/CustomerSupport.InternalApi/Controllers/AuthController.cs` (`change-password`), `Features/Auth/Commands/ChangePassword/*`.

**Step 1 — Real endpoint**

```csharp
// AuthController.cs
[HttpPost("change-password")]
[Authorize]
public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
{
    var userId = User.GetRequiredUserId();
    return this.ToActionResult(await _mediator.Send(
        new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword), ct));
}
```

- [ ] **Step 2: Run:** `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ChangePassword"`
Expected: PASS.

- [ ] **Step 3: Commit:** `git commit -m "feat(users): change own password (AUTH)"`

## Task 3 — Angular users screen (`AUTH-18`..`AUTH-22`)

**Files:**
- `frontend/projects/admin-app/src/app/features/users/users.component.ts`
- `frontend/projects/common/src/lib/auth/staff.api.ts`
- `frontend/projects/common/src/lib/auth/guards.ts` (`roleGuard('Admin')`)

**Interfaces:** `StaffApi.list()`, `StaffApi.create({...roles})`, `StaffApi.setActive(id, isActive)`; `SessionStore.roles()`.

**Step 1 — Real service (excerpt)**

```ts
// frontend/projects/common/src/lib/auth/staff.api.ts
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
```

The component renders the list via an `AsyncState` union (so an error cannot render as "no staff"), the create form posts `roles: [role]` where `role` defaults to `"User"` (the platform's real two-role vocabulary), and `fieldError(field)` lands a server `errors[]` on the right input. The route is guarded by `roleGuard('Admin')` — a courtesy; the `Admin` policy on the endpoints is the control (`AUTH-22`).

- [ ] **Step 2: Run:** `cd frontend && npx ng test admin-app --watch=false --filter users`
Expected: PASS — list/empty/error distinct; server field error binds to control.

- [ ] **Step 3: Commit:**

```bash
git add frontend/projects/admin-app/src/app/features/users/ \
        frontend/projects/common/src/lib/auth/staff.api.ts
git commit -m "feat(users): Angular staff list/create/activate (AUTH-18..AUTH-22)"
```

## Self-review

Coverage: `AUTH-1`–`AUTH-17` → Task 1; self-service password → Task 2; `AUTH-18`–`AUTH-22` → Task 3.

**Discrepancy found:** prose claimed `Supervisor` policy + lowercase `/api/users`; real code is `Admin` policy at `/api/Users`, and roles are `Admin`/`User`. The users UI itself documents this correction (`users.component.ts` comment: "the backend's actual two-role vocabulary (FE-2), not this feature's earlier Supervisor/Agent naming").
