# US-610 Report Filters: Implementation Plan

> **Disclosure (added 2026-08-27):** Rewritten to carry real, code-bearing Task sections. Report
> filters are NOT SHIPPED; the code below extends the three already-shipped report queries with
> shared `categoryId` / `priority` / `agentId` filter parameters, introduced through a single shared
> base validator so every report accepts them identically.

**Story:** `US-610` · **Spec:** `docs/superpowers/specs/EPIC-08-US-606-reporting.md` · **Status:** NOT SHIPPED

## AC mapping

| Story AC | Proof |
|---|---|
| AC1 — ticket-volume can be filtered by category and priority | `ReportFiltersTests.AC610_TicketVolume_FilterByCategoryAndPriority` |
| AC2 — agent-performance can be filtered by agentId | `ReportFiltersTests.AC610_AgentPerformance_FilterByAgent` |
| AC3 — invalid filter values return 400, not 500 | `ReportFiltersTests.AC610_BadPriority_Returns400` |

## Affected files

- Create: `backend/src/CustomerSupport.Application/Features/Reports/Queries/ReportFilter.cs` (shared)
- Modify: `.../GetTicketVolumeReport/GetTicketVolumeReportQuery.cs` + Handler + Validator
- Modify: `.../GetAgentPerformanceReport/GetAgentPerformanceReportQuery.cs` + Handler
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/ReportFiltersTests.cs`

---

### Task 1: Shared `ReportFilter` + ticket-volume filters (`AC-610.1`)

**Files:**
- Create: `.../Queries/ReportFilter.cs`
- Modify: `GetTicketVolumeReport*` and `ReportsController.GetTicketVolume`

**Interfaces:**
- Produces: `ReportFilter(Guid? CategoryId, string? Priority, Guid? AgentId)`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact] [Trait("AC", "610.1")]
public async Task AC610_TicketVolume_FilterByCategoryAndPriority()
{
    var cat = await CreateCategoryAsync("Billing");
    await CreateTicketAsync(categoryId: cat, priority: "High");
    await CreateTicketAsync(categoryId: cat, priority: "Low");
    var response = await _client.GetFromJsonAsync<Response<VolumeRow>>(
        $"/api/reports/ticket-volume?from={DateTime.UtcNow.AddDays(-7):o}&to={DateTime.UtcNow:o}&priority=High");
    response!.Data!.Rows.SelectMany(r => r.Buckets).Sum(b => b.Count).Should().Be(1);
}
```

- [ ] **Step 2: Shared filter + extend query**

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Queries/ReportFilter.cs
namespace CustomerSupport.Application.Features.Reports.Queries;

public record ReportFilter(Guid? CategoryId = null, string? Priority = null, Guid? AgentId = null);

public record GetTicketVolumeReportQuery(DateTime From, DateTime To, string GroupBy, ReportFilter? Filter = null)
    : IQuery<Response<TicketVolumeReportDto>>;
```

- [ ] **Step 3: Apply the predicate in the handler**

```csharp
// Inside GetTicketVolumeReportQueryHandler — extend the existing predicate:
var tickets = await ticketRepository.ListAsync(t =>
    t.CreatedAt >= request.From && t.CreatedAt <= request.To &&
    (request.Filter?.CategoryId == null || t.CategoryId == request.Filter.CategoryId) &&
    (request.Filter?.Priority == null || t.Priority == request.Filter.Priority), ct);
```

- [ ] **Step 4: Controller signature**

```csharp
public async Task<IActionResult> GetTicketVolume(
    [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string groupBy = "day",
    [FromQuery] Guid? categoryId = null, [FromQuery] string? priority = null,
    [FromQuery] Guid? agentId = null, CancellationToken ct = default)
    => this.ToActionResult(await mediator.Send(
        new GetTicketVolumeReportQuery(from, to, groupBy, new ReportFilter(categoryId, priority, agentId)), ct));
```

- [ ] **Step 5: Run to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportFiltersTests&FullyQualifiedName~AC610_TicketVolume"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Reports/Queries/ReportFilter.cs \
        backend/src/CustomerSupport.Application/Features/Reports/Queries/GetTicketVolumeReport/ \
        backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ReportFiltersTests.cs
git commit -m "feat(reports): shared ReportFilter on ticket-volume (AC-610.1)"
```

---

### Task 2: Agent filter + validation (`AC-610.2`, `AC-610.3`)

**Files:**
- Modify: `GetAgentPerformanceReport*` and `ReportsController.GetAgentPerformance`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] [Trait("AC", "610.2")]
public async Task AC610_AgentPerformance_FilterByAgent()
{
    var agent = await CreateAgentAsync();
    var response = await _client.GetFromJsonAsync<Response<AgentRow>>(
        $"/api/reports/agent-performance?from={DateTime.UtcNow.AddDays(-7):o}&to={DateTime.UtcNow:o}&agentId={agent}");
    response!.Data!.Rows.Should().OnlyContain(r => r.AgentId == agent);
}

[Fact] [Trait("AC", "610.3")]
public async Task AC610_BadPriority_Returns400()
{
    var response = await _client.GetAsync($"/api/reports/ticket-volume?from={DateTime.UtcNow.AddDays(-7):o}&to={DateTime.UtcNow:o}&priority=NotARealPriority");
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

- [ ] **Step 2: Apply agent filter in `GetAgentPerformanceReportQueryHandler` predicate; add a
  `ReportFilterValidator` (or extend each query's validator) enumerating allowed `Priority` values
  (`Low`/`Medium`/`High`/`Critical`) and rejecting others with `VALnnn`.

- [ ] **Step 3: Run to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportFiltersTests"`
Expected: PASS, 3/3.

- [ ] **Step 4: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Reports/Queries/GetAgentPerformanceReport/ \
        backend/src/CustomerSupport.Application/Features/Reports/Queries/ReportFilterValidator.cs \
        backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ReportFiltersTests.cs
git commit -m "feat(reports): agent filter + priority validation (AC-610.2, AC-610.3)"
```

## Definition of done

`AC-610.1`..`AC-610.3` covered by named tests · build clean · test run pasted. `US-608`'s scope
predicate plugs into the same `ReportFilter`.
