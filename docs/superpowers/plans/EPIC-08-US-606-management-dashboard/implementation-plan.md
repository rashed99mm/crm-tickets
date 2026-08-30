# US-606 Management Dashboard: Implementation Plan

> **Disclosure (added 2026-08-27):** Rewritten to carry real, code-bearing Task sections. The
> management dashboard described here is NOT SHIPPED; the code below reuses the three already-shipped
> report queries (`GetTicketVolumeReport`, `GetSlaPerformanceReport`, `GetAgentPerformanceReport`)
> and aggregates them behind one dashboard endpoint.

**Story:** `US-606` · **Spec:** `docs/superpowers/specs/EPIC-08-US-606-reporting.md` · **Status:** NOT SHIPPED

## AC mapping

| Story AC | Proof |
|---|---|
| AC1 — single dashboard call returns volume + SLA + agent + CSAT summary | `ManagementDashboardTests.AC606_Dashboard_ReturnsAllFourSummaries` |
| AC2 — respect the same date range and Supervisor auth | `ManagementDashboardTests.AC606_Dashboard_RequiresSupervisor` |

## Affected files

- Create: `backend/src/CustomerSupport.Application/Features/Reports/Queries/GetManagementDashboard/`
- Create: `backend/src/CustomerSupport.Application/Features/Reports/Dtos/ManagementDashboardDto.cs`
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/ManagementDashboardTests.cs`

---

### Task 1: Aggregate dashboard DTO + handler (`AC-606.1`)

**Files:**
- Create: `.../Dtos/ManagementDashboardDto.cs`
- Create: `.../Queries/GetManagementDashboard/GetManagementDashboardQuery.cs` + Handler
- Modify: `ReportsController.cs`

**Interfaces:**
- Consumes: the three existing report handlers via `IMediator.Send(...)` (no re-querying — reuse).
- Produces: `ManagementDashboardDto(TicketVolumeReportDto Volume, SlaPerformanceReportDto Sla, AgentPerformanceReportDto Agents, CsatSummaryDto? Csat)`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact] [Trait("AC", "606.1")]
public async Task AC606_Dashboard_ReturnsAllFourSummaries()
{
    var from = DateTime.UtcNow.AddDays(-7); var to = DateTime.UtcNow;
    var response = await _client.GetAsync($"/api/reports/dashboard?from={from:o}&to={to:o}");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var dash = (await response.Content.ReadFromJsonAsync<Response<DashboardRow>>())!.Data!;
    dash.Volume.Should().NotBeNull();
    dash.Sla.Should().NotBeNull();
    dash.Agents.Should().NotBeNull();
}
```

- [ ] **Step 2: DTO + query**

```csharp
public record ManagementDashboardDto(
    TicketVolumeReportDto Volume, SlaPerformanceReportDto Sla,
    AgentPerformanceReportDto Agents, object? Csat);

public record GetManagementDashboardQuery(DateTime From, DateTime To) : IQuery<Response<ManagementDashboardDto>>;
```

- [ ] **Step 3: Handler reuses existing reports**

```csharp
public class GetManagementDashboardQueryHandler(IMediator mediator, IMessageFactory messages)
    : IQueryHandler<GetManagementDashboardQuery, Response<ManagementDashboardDto>>
{
    public async Task<Response<ManagementDashboardDto>> Handle(GetManagementDashboardQuery q, CancellationToken ct)
    {
        var (vol, sla, agents) = await (
            mediator.Send(new GetTicketVolumeReportQuery(q.From, q.To, "day"), ct),
            mediator.Send(new GetSlaPerformanceReportQuery(q.From, q.To), ct),
            mediator.Send(new GetAgentPerformanceReportQuery(q.From, q.To), ct));
        var dto = new ManagementDashboardDto(vol.Data!, sla.Data!, agents.Data!, null);
        return messages.Success(dto, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
```

- [ ] **Step 4: Controller action (inherits `[Authorize(Policy = "Supervisor")]`)**

```csharp
[HttpGet("dashboard")]
[ProducesResponseType(typeof(Response<ManagementDashboardDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetDashboard([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    => this.ToActionResult(await mediator.Send(new GetManagementDashboardQuery(from, to), ct));
```

- [ ] **Step 5: Run to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ManagementDashboardTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Reports/Queries/GetManagementDashboard/ \
        backend/src/CustomerSupport.Application/Features/Reports/Dtos/ManagementDashboardDto.cs \
        backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ManagementDashboardTests.cs
git commit -m "feat(reports): management dashboard aggregate (AC-606.1)"
```

---

### Task 2: Authorization reuse (`AC-606.2`)

**Files:** none new — the controller's class-level `[Authorize(Policy = "Supervisor")]` already covers the new action.

- [ ] **Step 1: Write the auth test**

```csharp
[Fact] [Trait("AC", "606.2")]
public async Task AC606_Dashboard_RequiresSupervisor()
{
    var anon = _factory.CreateClient();
    var response = await anon.GetAsync($"/api/reports/dashboard?from={DateTime.UtcNow.AddDays(-1):o}&to={DateTime.UtcNow:o}");
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

- [ ] **Step 2: Run**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ManagementDashboardTests&FullyQualifiedName~AC606_Dashboard_RequiresSupervisor"`
Expected: PASS (no code change; policy already on the controller).

- [ ] **Step 3: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/ManagementDashboardTests.cs
git commit -m "test(reports): dashboard authorization assertion (AC-606.2)"
```

## Definition of done

`AC-606.1`, `AC-606.2` covered by named tests · build clean · test run pasted. No new auth code.
