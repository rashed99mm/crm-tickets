# US-306 — Branch-Scoped Query Filters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.

> **Current implementation:** See [as-built-execution-plan.md](./as-built-execution-plan.md). The
> original claim-based design below is historical planning context; the shipped policy resolves
> `BranchId` from the authenticated user record.

**Goal:** A branch-scoped user (one with a `BranchId`) sees only their branch's tickets and customers; an
unscoped (admin-level) user sees everything — `BR-21`.

**This plan is blocked on open question `OQ-5` and is written as a design-with-a-gate, not a normal TDD
cycle.** The two real prerequisites are named in `docs/product/05-assumptions-and-open-questions.md`:
1. **`OQ-5`**: what "branch scoping" actually means — per-user, per-role, opt-in? This plan assumes the
   simplest resolution (a non-null `branch_id` claim on the JWT scopes the viewer to that branch) and is
   explicitly *marked `OQ-5 GATE`* at every step that would need revising if `OQ-5` lands differently.
2. **Nothing in the codebase ever populates `Ticket.BranchId` / `Customer.BranchId`** (`FEAT-16`'s own gap,
   `US-303`/`US-304`). A filter over a column that is always `NULL` is a query with a passing test and
   zero real effect — the anti-pattern `FEAT-16`'s spec (A1) already refused for `US-608`. So `AC-222` is
   written as a *named, reasoned `Skip`*, not silently omitted.

**Confirmed blocker fact (read from code, not guessed):** `TokenService.GenerateAccessToken`
(`backend/src/CustomerSupport.Infrastructure/Security/TokenService.cs`) emits only `NameIdentifier`,
`Email`, `Sub`, `Jti` and `Role` claims — there is **no `branch_id` claim issuer anywhere today**. So even
after the filter code below lands, no real token will ever carry a branch claim until `OQ-5` is answered
*and* the issuer is extended. The filter is correct and reviewable; it simply cannot be proven against
real data until then.

**Spec:** none dedicated — `FEAT-16`'s own spec (`EPIC-13-US-311-organisation-structure.md`) does not
cover this story; it is covered only by `US-306`'s own story file and the umbrella
`EPIC-13-US-311-gap-closure-program.md`.

## Acceptance criteria (continuing from `US-220`'s `AC-243` — next free is `AC-244`)

AC-244. **[OQ-5 GATE]** Given an authenticated user whose token carries a non-null `BranchId` claim, when
they query `GET /api/Tickets` or `GET /api/Customers`, then only rows whose `BranchId` matches the claim
are returned. *Implemented below; proven only once OQ-5 is answered and a claim is issued (see `AC-222`
in the prior numbering, here re-lettered to `AC-244` for continuity).*

AC-245. Given an authenticated user whose token carries no `BranchId` claim (the platform's current
default for every seeded user), then the query is unfiltered by branch, exactly as today.

AC-246. Given the branch filter and an existing filter (e.g. `status`) are both supplied, then both apply
together (`AND`), not one replacing the other.

**A fourth criterion this plan deliberately does not write:** "a branch-scoped user querying tickets sees
only their branch's tickets, proven against real data" — cannot be written honestly today, because no
seeded or creatable ticket has a non-null `BranchId` to prove the filter against (the value doesn't stop
`NULL == NULL` from matching under SQL either, which is a correctness trap of its own — see Task 1, Step 3's
note). `AC-244` is provable only once something else assigns `BranchId` to a real row, which is `OQ-5`'s
territory, not this plan's.

## Global Constraints

- No new error codes, no new endpoint — this is a filter predicate change to two existing queries.
- `IUserContext` (`Application/Interfaces/IUserContext.cs`) already exposes `GetClaim(string claimType)` and
  is **already injected** into `GetTicketsQueryHandler` (constructor has `IUserContext userContext`). For
  `GetCustomersQueryHandler` it is **not** yet injected, so that handler's constructor gains it. Reading
  the branch through `GetClaim("branch_id")` (not a new interface member) keeps this small and reversible if
  `OQ-5` lands on a different shape entirely.

---

### Task 1: Branch claim + filter on `GetTicketsQuery`/customer query (`OQ-5 GATE`)

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQueryHandler.cs`
- Modify: `backend/src/CustomerSupport.Application/Features/Customers/Queries/GetCustomers/GetCustomersQueryHandler.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/BranchScopedQueryTests.cs`

**Interfaces:**
- Consumes: `IUserContext.GetClaim("branch_id")` (existing method, new claim key — the JWT issuer does not
  currently emit this claim for anyone; see the `OQ-5 GATE` header).

> **OQ-5 GATE — do not execute Step 3's claim-issuance assumption unilaterally.** The filter below is safe
> to merge regardless of `OQ-5`'s answer (it only activates when a `branch_id` claim is present). The one
> thing this plan does NOT do is answer `OQ-5` by inventing a claim schema — if `OQ-5` resolves to
> per-role or opt-in scoping, the `GetClaim("branch_id")` read is the single line to revise.

- [ ] **Step 1: Write the failing test — and read it as documentation of the current blocker**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/BranchScopedQueryTests.cs
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

public class BranchScopedQueryTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;
    private Guid _categoryId, _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
        _categoryId = await _factory.EnsureCategoryAsync("Technical");
        var c = await _admin.PostAsJsonAsync("/api/Customers", new { name="BS", email=$"bs-{Guid.NewGuid():N}@e.com", phone=(string?)null });
        _customerId = (await c.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }
    public Task DisposeAsync() { _admin.Dispose(); return _factory.DisposeAsync().AsTask(); }

    private async Task<Guid> CreateTicketAsync()
    {
        var r = await _admin.PostAsJsonAsync("/api/Tickets", new
        {
            subject = $"BS {Guid.NewGuid():N}", description = "x",
            customerId = _customerId, categoryId = _categoryId, priority = "Normal",
        });
        return (await r.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    [Fact]
    [Trait("AC", "245")]
    public async Task AC245_UserWithNoBranchClaim_SeesAllTickets()
    {
        await CreateTicketAsync();

        var response = await _admin.GetFromJsonAsync<Response<PaginatedList<TicketRow>>>("/api/Tickets");

        response!.Data!.TotalCount.Should().BeGreaterThanOrEqualTo(1); // unfiltered — the only provable case today
    }

    // OQ-5 GATE — blocked: no fixture can mint a token with a non-null branch_id claim (no seeded user
    // has BranchId set, and TokenService never emits the claim), and no row has a non-null BranchId to
    // filter against. Named, not silent, so the gap is visible in test output.
    [Fact(Skip = "Blocked on OQ-5: no branch_id claim is issued by TokenService and no Ticket/BranchId is populated by any path (US-303/304). Un-Skip once OQ-5 is answered and the issuer emits the claim.")]
    [Trait("AC", "244")]
    public async Task AC244_UserWithBranchClaim_SeesOnlyOwnBranchTickets()
    {
    }
}
```

- [ ] **Step 2: Run test to verify the provable case passes and the skip is visible**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~BranchScopedQueryTests"`
Expected: `AC245_*` passes once the filter code below exists (it trivially passes before the code change
too, since "no filter" is also the current, unfiltered behavior — the real value of this step is
confirming the skip reason renders in the test output, not a red/green transition).

- [ ] **Step 3: Add the filter**

In `GetTicketsQueryHandler.cs`, extend the existing `PredicateBuilder` chain (the handler already injects
`IUserContext userContext`):

```csharp
        var branchId = userContext.GetClaim("branch_id") is { } raw && Guid.TryParse(raw, out var parsed)
            ? parsed
            : (Guid?)null;

        var filter = PredicateBuilder.True<Ticket>()
            .WhereIf(!string.IsNullOrWhiteSpace(request.Status), t => t.Status == request.Status!)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Priority), t => t.Priority == request.Priority!)
            .WhereIf(request.CustomerId.HasValue, t => t.CustomerId == request.CustomerId!.Value)
            .WhereIf(request.Unassigned && !request.Mine, t => t.AssigneeId == null)
            .WhereIf(assigneeId.HasValue, t => t.AssigneeId == assigneeId!.Value)
            // US-306, AC-244/245/246 — only narrows when a branch claim is present (WhereIf guards the
            // unscoped case); t.BranchId == branchId.Value correctly excludes NULL-BranchId rows when a
            // branch filter IS active (SQL NULL = 'x' is never true).
            .WhereIf(branchId.HasValue, t => t.BranchId == branchId!.Value);
```

Apply the identical pattern to `GetCustomersQueryHandler.cs` — first add `IUserContext userContext` to its
constructor (today it is `(IRepository<Customer> customers)`), then:

```csharp
        var branchId = userContext.GetClaim("branch_id") is { } raw && Guid.TryParse(raw, out var parsed)
            ? parsed
            : (Guid?)null;

        var filter = PredicateBuilder.True<Customer>()
            .WhereIf(!string.IsNullOrWhiteSpace(request.Search),
                c => c.Name.Contains(request.Search!) || c.Email.Contains(request.Search!))
            .WhereIf(branchId.HasValue, c => c.BranchId == branchId!.Value);
```

(`Customer.BranchId` is the nullable column already added by `FEAT-16` — confirmed on
`ApplicationUser`/`Customer` alongside `DepartmentId`; if `Customer` does not yet carry `BranchId`, that
is itself an `OQ-5`/US-303 prerequisite and must be added first via a migration, which this plan's `OQ-5
GATE` is precisely flagging.)

- [ ] **Step 4: The real prerequisite this plan cannot close — recorded, not solved**

Nothing issues a `branch_id` claim today. Closing that requires: (a) `OQ-5` answered, (b)
`ApplicationUser.BranchId`/`Customer.BranchId`/`Ticket.BranchId` populated for at least one real user/rows
(which itself depends on `US-303`/`US-304`'s columns being written to by something — currently nothing
writes them), and (c) `TokenService.GenerateAccessToken` adding `new Claim("branch_id", user.BranchId?.ToString() ?? string.Empty)`
when that value is non-null. All three are named here as blocking prerequisites, not attempted by this
task — attempting them would mean answering `OQ-5` unilaterally, which is a product decision, not an
engineering one. The `AC-244` `Skip` is removed only after (c) is real and a fixture can mint a
branch-scoped token.

- [ ] **Step 5: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQueryHandler.cs \
        backend/src/CustomerSupport.Application/Features/Customers/Queries/GetCustomers/GetCustomersQueryHandler.cs \
        backend/tests/CustomerSupport.Tests/Integration/BranchScopedQueryTests.cs
git commit -m "feat(tickets,customers): branch-scoped query filter, OQ-5 GATE for real proof (US-306, AC-244..246)"
```

## Definition of done

**Cannot be fully met** until `OQ-5` is answered and something populates `BranchId` — this plan's own
honest limitation, stated in its header rather than discovered by a reviewer later. What can be done:
`AC-245` (the unscoped case) proven; `AC-244`/`AC-246` implemented and covered by a named, reasoned
`Skip` rather than silently absent; a task record written to
`docs/superpowers/plans/EPIC-13-US-306-branch-scoped-queries/README.md` that says exactly this, with the `OQ-5
GATE` line repeated at the top so an executor does not merge it thinking the feature is live.
