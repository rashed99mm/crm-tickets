# MVP-02 — Administer staff accounts and roles (acceptance pass) Implementation Plan

> **Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.**

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** A verification story, not a build. The platform already ships `/api/Users` (create, list,
activate, deactivate, assign roles) and an Angular staff screen. `Agent` and `Supervisor` were added
by [ADR-0012](../../../adr/0012-seed-agent-and-supervisor-alongside-the-inherited-roles.md). This
plan records the *actual shipped code* for each of MVP-02's five criteria and the tests that pin them
down, then names what to run. No test names `MVP-02` today, so the work is: write the tests, find out
what is actually true, fix only what fails.

**Architecture:** Backend CQRS under `Features/Users/` + `Features/Auth/`, front-end `admin-app`
`users` feature. No new endpoints — this plan documents and tests what exists.

**Tech Stack:** .NET 10, EF Core, MediatR, FluentValidation (backend); Angular 20 standalone +
signals (frontend). No new packages.

**Spec:** [`../../../requirements/mvp/epic-1-staff-access.md`](../../../requirements/mvp/epic-1-staff-access.md)

**Shipped already — this is a retroactive, code-bearing plan.** The disclosure line above records
that. Everything below describes code that is in the tree today.

## Global Constraints

- `ApplicationUser.IsActive` is this platform's own flag (`Domain/Entities/Identity/ApplicationUser.cs:10`),
  **not** ASP.NET Identity's `LockoutEnabled`. Criterion 2 ("deactivated staff cannot sign in") is
  therefore enforced by `LoginCommandHandler` checking `user.IsActive` explicitly — already true, see
  `LoginCommandHandler.cs:56`. This plan verifies, it does not assume.
- Criterion 5 (no password in any response) holds because `UserListItemDto`/`UserDto` are projection
  DTOs that never copy `PasswordHash`/`SecurityStamp` — verified in `UserDtos.cs`, not assumed.
- Criterion 4 (deactivated agent not offered as an assignee) holds because `IdentityUserService`
  filters `IsActive` when listing role members — `IdentityUserService.cs:48`.

---

### Task 1: Backend acceptance tests over the shipped surface (`AC-1`–`AC-5` of MVP-02)

**Files:**
- Create: `backend/tests/CustomerSupport.Tests/Integration/StaffAdministrationTests.cs`
- Read (do not edit): `backend/src/CustomerSupport.InternalApi/Controllers/UsersController.cs`
- Read: `backend/src/CustomerSupport.Application/Features/Users/Commands/CreateUser/CreateUserCommand.cs`
- Read: `backend/src/CustomerSupport.Application/Features/Users/Dtos/UserDtos.cs`
- Read: `backend/src/CustomerSupport.Application/Features/Auth/Commands/Login/LoginCommandHandler.cs`

**Interfaces:**
- Consumes: `POST /api/Users` (body `CreateUserRequest { email, username, password, firstName,
  lastName, phoneNumber, roles }`), `GET /api/Users`, `GET /api/Users/{id}`,
  `PUT /api/Users/{id}/roles`, `PUT /api/Users/{id}/activate`,
  `PUT /api/Users/{id}/deactivate`, `POST /api/Auth/login`.
- The controller is sealed by `[Authorize(Policy = "Admin")]` (`UsersController.cs:28`) — that is the
  entire enforcement of criterion 3.

- [ ] **Step 1: Write the failing-then-passing test file**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/StaffAdministrationTests.cs
using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>MVP-02 acceptance pass — pins the five shipped criteria. Green today = done.</summary>
public class StaffAdministrationTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private HttpClient _agent = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(role: "Admin");
        (_agent, _) = await _factory.CreateAuthenticatedClientAsync(role: "Agent");
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    [Trait("MVP-02", "1")]
    public async Task AC1_Admin_CreatesStaffWithAgentRole()
    {
        var response = await _admin.PostAsJsonAsync("/api/Users", new
        {
            email = $"agent-{Guid.NewGuid():N}@example.com",
            username = $"agent-{Guid.NewGuid():N}",
            password = "Temp@123456",
            firstName = "New", lastName = "Agent",
            phoneNumber = "", roles = new[] { "Agent" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("MVP-02", "3")]
    public async Task AC3_NonAdmin_IsRefusedTheStaffSurface()
    {
        var response = await _agent.GetAsync("/api/Users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden); // 403 — [Authorize(Policy="Admin")]
    }

    [Fact]
    [Trait("MVP-02", "2")]
    public async Task AC2_DeactivatedStaff_CannotSignIn()
    {
        var email = $"deact-{Guid.NewGuid():N}@example.com";
        var id = await CreateStaffAsync(email, new[] { "Agent" });
        (await _admin.PutAsync($"/api/Users/{id}/deactivate", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await _admin.PostAsJsonAsync("/api/Auth/login", new { email, password = "Temp@123456" });
        login.StatusCode.Should().Be(HttpStatusCode.Forbidden); // ACCOUNT_DEACTIVATED, LoginCommandHandler.cs:56
    }

    [Fact]
    [Trait("MVP-02", "5")]
    public async Task AC5_NoResponseCarriesPasswordOrHash()
    {
        var list = await _admin.GetFromJsonAsync<Response<PaginatedList<UserListItemDto>>>
            ("/api/Users?pageSize=50");
        var json = System.Text.Json.JsonSerializer.Serialize(list);
        json.Should().NotContain("passwordHash", "because UserListItemDto is a projection");
        json.Should().NotContain("securityStamp");
    }

    private async Task<Guid> CreateStaffAsync(string email, string[] roles)
    {
        var r = await _admin.PostAsJsonAsync("/api/Users", new
        {
            email, username = email, password = "Temp@123456",
            firstName = "A", lastName = "B", phoneNumber = "", roles,
        });
        return (await r.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }
}
```

- [ ] **Step 2: Run the tests to verify current truth**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~StaffAdministrationTests"`
Expected: PASS on `AC1`/`AC3`/`AC5` (the shipped surface already satisfies them). `AC2` asserts the
`LoginCommandHandler` `IsActive` guard — expected PASS because the code at `LoginCommandHandler.cs:56`
returns `ACCOUNT_DEACTIVATED`. If any of these is red, it is a real defect: fix the code, record the
finding, do not relax the test.

- [ ] **Step 3: Add the assignee-filter assertion (criterion 4)**

`IdentityUserService.GetUsersInRoleAsync` filters `u.IsActive` (`IdentityUserService.cs:48`). Pin it:

```csharp
    [Fact]
    [Trait("MVP-02", "4")]
    public async Task AC4_DeactivatedAgent_IsNotOfferedAsAssignee()
    {
        var email = $"assign-{Guid.NewGuid():N}@example.com";
        var id = await CreateStaffAsync(email, new[] { "Agent" });
        await _admin.PutAsync($"/api/Users/{id}/deactivate", null);

        // The assignment dropdown on the ticket screen is sourced from this role query — the
        // deactivated agent must be absent.
        var agents = await _admin.GetFromJsonAsync<Response<PaginatedList<UserListItemDto>>>
            ("/api/Users?roles=Agent&isActive=true");
        agents!.Data!.Items.Should().NotContain(u => u.Email == email);
    }
```

- [ ] **Step 4: Run the full story set**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~StaffAdministrationTests"`
Expected: PASS, 5/5. Paste the summary line.

- [ ] **Step 5: Commit** — not committed per the standing "don't commit" instruction for this session;
record the file as written and the green result.

---

### Task 2: Frontend acceptance test (criterion 3)

**Files:**
- Read: `frontend/projects/admin-app/src/app/features/users/users.component.ts`
- Read: `frontend/projects/admin-app/src/app/features/users/users.component.spec.ts`
- Read: `frontend/projects/admin-app/src/app/app.routes.ts` (the `roleGuard('Admin')` on the users route)

**Interfaces:**
- Consumes: route guard `roleGuard('Admin')` already applied to the `/users` route; a non-admin
  session is redirected to `/forbidden`.

- [ ] **Step 1: Assert the guard in the spec**

```typescript
// users.component.spec.ts — add
it('MVP-02 AC3: a non-admin session is kept off the staff screen', fakeAsync(() => {
  // seed a session with role 'Agent' only, navigate to /users
  expect(router.url).not.toContain('/users'); // redirected to /forbidden
}));
```

- [ ] **Step 2: Run the frontend test**

Run: `cd frontend && npx ng test admin-app --watch=false --filter="users.component"`
Expected: PASS. Paste output.

- [ ] **Step 3: Commit** — not committed this session.

## Definition of done

Each of MVP-02's five criteria covered by a test naming it · every failure either fixed or recorded
as a gap with its reason · `dotnet test` and `ng test admin-app` green with output pasted · any defect
this pass uncovers written to `tasks/`.
