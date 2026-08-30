> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# US-805 Permission Admin UI: Implementation Plan

> **Disclosure (added 2026-08-27):** Rewritten to carry real, code-bearing Task sections. The
> permission administration **backend (`PermissionsController`) and frontend (`permissions.component`)
> already exist in the tree** (shipped during the admin/authorization passes). This plan now quotes
> that shipped code accurately. The cited ACs remain **unverified by a named test** — that is the
> residual gap, not the absence of implementation.

**Story:** `EPIC-09-US-805-permission-admin-ui`
**Spec:** `docs/superpowers/specs/EPIC-09-EPIC-09-US-805-permission-admin-ui.md`
**Status:** PARTIAL — implementation exists in tree (unverified by a named test).

## Affected files (real)

- `backend/src/CustomerSupport.InternalApi/Controllers/PermissionsController.cs`
- `backend/src/CustomerSupport.Application/Features/Admin/Commands/AssignPermission/`
- `backend/src/CustomerSupport.Application/Features/Admin/Commands/RevokePermission/`
- `backend/src/CustomerSupport.Application/Features/Admin/Queries/GetPermissions/`
- `frontend/projects/admin-app/src/app/features/admin/permissions.component.ts` (+ `.html`, `.spec.ts`)

---

### Task 1: The `PermissionsController` (shipped, quoted) — `AC-805.1`

**Files:**
- Real: `backend/src/CustomerSupport.InternalApi/Controllers/PermissionsController.cs`

- [ ] **Step 1: Real controller code (in tree)**

```csharp
[ApiController]
[Route("api/admin/permissions")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "UserManagement")]   // Admin-only gate, per AuthorizationExtensions
public sealed class PermissionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]   public async Task<IActionResult> List(CancellationToken ct)
        => this.ToActionResult(await mediator.Send(new GetPermissionsQuery(), ct));

    [HttpPost("{roleId:guid}/{permissionId:guid}")]
    public async Task<IActionResult> Assign(Guid roleId, Guid permissionId, CancellationToken ct)
        => this.ToActionResult(await mediator.Send(new AssignPermissionCommand(roleId, permissionId), ct));

    [HttpDelete("{roleId:guid}/{permissionId:guid}")]
    public async Task<IActionResult> Revoke(Guid roleId, Guid permissionId, CancellationToken ct)
        => this.ToActionResult(await mediator.Send(new RevokePermissionCommand(roleId, permissionId), ct));
}
```

- [ ] **Step 2: No production change required** — the controller is in the tree.

- [ ] **Step 3: Residual — named API test**

```csharp
[Fact] [Trait("AC", "805.1")]
public async Task AC805_1_AssignPermission_Admin_Succeeds()
{
    var (roleId, permId) = await SeedRoleAndPermissionAsync();
    var response = await _client.PostAsync($"/api/admin/permissions/{roleId}/{permId}", null);
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact] [Trait("AC", "805.1")]
public async Task AC805_1_AssignPermission_NonAdmin_Returns403()
{
    var agent = _factory.CreateAuthenticatedClient("Agent");
    var response = await agent.PostAsync($"/api/admin/permissions/{Guid.NewGuid()}/{Guid.NewGuid()}", null);
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~PermissionsEndpointTests"`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/PermissionsEndpointTests.cs
git commit -m "test(permissions): admin assign/revoke API tests (AC-805.1)"
```

---

### Task 2: The `permissions` component (shipped, quoted) — `AC-805.2`, `AC-805.3`

**Files:**
- Real: `frontend/projects/admin-app/src/app/features/admin/permissions.component.ts`

- [ ] **Step 1: Real component code (in tree)**

```ts
export default class PermissionsComponent {
  private readonly api = inject(PermissionApi);
  readonly state = signal<AsyncState<PermissionAdministration>>(loading());
  readonly mutating = signal<string | null>(null);
  readonly mutationError = signal<ApiError | null>(null);
  readonly mutationSuccess = signal<'assigned' | 'revoked' | null>(null);

  load(): void {
    this.state.set(loading());
    this.api.list().subscribe({
      next: (result) => this.state.set(result.permissions.length ? { status: 'loaded', data: result } : { status: 'empty' }),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  toggle(role: PermissionAdministrationRole, permission: PermissionAdministrationPermission, checked: boolean): void {
    const key = `${role.id}:${permission.id}`;
    if (this.mutating()) return;
    this.mutating.set(key);
    const request = checked ? this.api.assign(role.id, permission.id) : this.api.revoke(role.id, permission.id);
    request.subscribe({
      next: () => { this.mutating.set(null); this.mutationSuccess.set(checked ? 'assigned' : 'revoked'); this.load(); },
      error: (error: unknown) => { this.mutating.set(null); this.mutationError.set(this.toApiError(error)); },
    });
  }
}
```

- [ ] **Step 2: No production change required** — the component is in the tree.

- [ ] **Step 3: Residual — named component test**

```ts
it('US805_TogglePermission_CallsAssignThenRevokes', () => {
  // stub PermissionApi; assert assign called on checked=true, revoke on checked=false,
  // and that mutationSuccess flips accordingly and load() is re-invoked.
});
```

Run: `cd frontend && npx ng test admin-app --watch=false --include "**/permissions.component.spec.ts"`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/admin/permissions.component.spec.ts
git commit -m "test(permissions): admin UI toggle specs (AC-805.2, AC-805.3)"
```

## Definition of done

- [x] `PermissionsController` + Assign/Revoke/List commands in tree (`AC-805.1`).
- [x] `permissions.component.ts` (OnPush, signal-based toggle) in tree (`AC-805.2`, `AC-805.3`).
- [ ] Cited ACs verified by named tests (Task 1/2 residual tests close the gap).
- [x] Dependency rule intact: controller depends on `Application` MediatR only.

## Deviation record

Implementation preceded this plan (admin/authorization passes). Residual item is story-named test
verification; this rewrite adds those tests as the closing task.

