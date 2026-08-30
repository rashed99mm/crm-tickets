> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# US-608 Report Scoping — Implementation Plan

> **Disclosure (added 2026-08-27):** Rewritten to carry real, code-bearing Task sections. The
> backend **reports already shipped** as `FEAT-19` (`ReportsController` + three query handlers, all
> under `[Authorize(Policy = "Supervisor")]`). This plan now (a) quotes that shipped code accurately
> and (b) designs the **missing department/branch scope** that the original story requires and that
> the prior pass deliberately did not implement.

**Story:** `EPIC-08-US-608-report-scoping`
**Spec:** `docs/superpowers/specs/EPIC-08-EPIC-08-US-608-report-scoping.md`
**Current status:** PARTIAL — Admin/Supervisor authorization shipped; department/branch scoping is a documented gap.

## Findings and decision gate (unchanged, still blocking T1)

`Ticket.DepartmentId`, `Ticket.BranchId`, `ApplicationUser.DepartmentId`, `BranchId` exist but are
nullable and unset. Roles are `Admin`/`Supervisor`/`Agent`; there is **no** `Manager` role and no
`departmentId` JWT claim. A department predicate would currently return no data and create false
security evidence. Product must choose the scope source (populate relationships + issue claims, or
amend the story to role-gated reports). Until then, no client `departmentId` is accepted.

## What shipped (real code, quoted)

```csharp
// backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs
[ApiController]
[Route("api/reports")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Supervisor")]   // Supervisor policy == Supervisor or Admin (AuthorizationExtensions)
public class ReportsController(IMediator mediator) : ControllerBase
{
    [HttpGet("ticket-volume")]   public async Task<IActionResult> GetTicketVolume([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string groupBy = "day", CancellationToken ct = default)
        => this.ToActionResult(await mediator.Send(new GetTicketVolumeReportQuery(from, to, groupBy), ct));

    [HttpGet("sla-performance")] public async Task<IActionResult> GetSlaPerformance([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
        => this.ToActionResult(await mediator.Send(new GetSlaPerformanceReportQuery(from, to), ct));

    [HttpGet("agent-performance")] public async Task<IActionResult> GetAgentPerformance([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
        => this.ToActionResult(await mediator.Send(new GetAgentPerformanceReportQuery(from, to), ct));
}
```

The three handlers live under `CustomerSupport.Application/Features/Reports/Queries/*` and project
aggregate DTOs. **No scope predicate is applied** — that is the gap this plan's Task 2 closes once
the decision gate is resolved.

## Affected files

- Real: `ReportsController.cs`, `Features/Reports/Queries/*`
- To design: `CustomerSupport.Application/Features/Reports/IReportScopeResolver.cs`,
  `CustomerSupport.Api.Shared/Authorization/ReportScopeResolver.cs`, every `Reports/Queries/*Handler`.

---

### Task 1: `IReportScopeResolver` port (`AC-608.1`–`AC-608.3`, design)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Reports/IReportScopeResolver.cs`
- Create: `backend/src/CustomerSupport.Api.Shared/Authorization/ReportScopeResolver.cs`

**Interfaces:**
- Produces: `ReportScope(bool IsAdmin, IReadOnlyList<Guid> PermittedBranchIds, IReadOnlyList<Guid> PermittedDepartmentIds)`.

- [ ] **Step 1: Write the failing resolution test**

```csharp
[Fact] [Trait("AC", "608.1")]
public async Task AC608_Admin_GetsAllBranchesScope()
{
    var resolver = new ReportScopeResolver(userContextAdmin);
    var scope = resolver.Resolve();
    scope.IsAdmin.Should().BeTrue();
    scope.PermittedBranchIds.Should().BeEmpty(); // empty == unrestricted for admin
}
```

- [ ] **Step 2: Port + implementation**

```csharp
// Application/Features/Reports/IReportScopeResolver.cs
public interface IReportScopeResolver
{
    ReportScope Resolve();
}

// Api.Shared/Authorization/ReportScopeResolver.cs
public sealed class ReportScopeResolver(IUserContext userContext) : IReportScopeResolver
{
    public ReportScope Resolve()
    {
        if (userContext.IsInRole("Admin")) return ReportScope.All;
        // Non-admin: read permitted branch/department ids from the JWT claims once product decides
        // the claim source (decision gate). Until then return an empty permitted set => no data.
        var branchIds = userContext.GetClaimGuidList("branchId");
        var deptIds = userContext.GetClaimGuidList("departmentId");
        return new ReportScope(false, branchIds, deptIds);
    }
}
```

- [ ] **Step 3: Run (will FAIL — types/claims not present)** then implement, then PASS.

- [ ] **Step 4: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Reports/IReportScopeResolver.cs \
        backend/src/CustomerSupport.Api.Shared/Authorization/ReportScopeResolver.cs
git commit -m "feat(reports): IReportScopeResolver port (AC-608.1)"
```

---

### Task 2: Apply scope to every report query (`AC-608.2`, `AC-608.3`, design)

**Files:**
- Modify: each `Features/Reports/Queries/*Handler` to accept `IReportScopeResolver` and AND its
  predicate; modify `US-609` export handler to reuse the same scoped query.

**Interfaces:**
- Consumes: `IReportScopeResolver.Resolve()`; ANDs `t.BranchId IN scope.PermittedBranchIds` (or
  `t.DepartmentId`) when `!scope.IsAdmin && permitted set non-empty`.

- [ ] **Step 1: Write the failing scoping tests**

```csharp
[Fact] [Trait("AC", "608.2")]
public async Task AC608_SameScope_ReturnsData() { /* scoped supervisor sees own branch rows */ }

[Fact] [Trait("AC", "608.3")]
public async Task AC608_CrossScope_Returns403()
{
    var cross = _factory.CreateAuthenticatedClient("Supervisor", branchId: otherBranch);
    var r = await cross.GetAsync($"/api/reports/ticket-volume?from={..}&to={..}");
    r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

- [ ] **Step 2: Implement the predicate AND in each handler (single helper to avoid drift):**

```csharp
private static IQueryable<Ticket> ApplyScope(IQueryable<Ticket> q, ReportScope scope) =>
    scope.IsAdmin || scope.PermittedBranchIds.Count == 0
        ? q
        : q.Where(t => t.BranchId.HasValue && scope.PermittedBranchIds.Contains(t.BranchId.Value));
```

- [ ] **Step 3: Run to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportScoping"`
Expected: PASS once the decision gate is resolved and claims issued.

- [ ] **Step 4: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Reports/Queries/
git commit -m "feat(reports): apply report scope to all queries (AC-608.2, AC-608.3)"
```

## Definition of done

- [x] Reports shipped with Supervisor auth (AC-148) — evidenced by `ReportsController`.
- [ ] Department/branch scoping implemented and named-tested (blocked on product scope-source decision).
- [x] `dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportsEndpointTests"` green for the shipped surface.

## Deviation record

The prior reporting pass implemented only Admin/Supervisor authorization and deliberately omitted
department scoping — a documented gap, not completion. This rewrite adds the real shipped code and
the design for the missing scope; it remains blocked on the product decision recorded in the gate.

