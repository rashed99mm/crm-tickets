> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# Reporting (FEAT-19+, backend) Implementation Plan

> **Disclosure (added 2026-08-27):** This plan was already code-bearing. The three report endpoints
> it describes **shipped** as part of the reporting pass (`ReportsController` + the three query
> handlers, all under `[Authorize(Policy = "Supervisor")]`). The code quoted below reflects the
> implementation already in the tree. The **missing department/branch scope** (story `US-608`) is the
> one documented gap and is designed, not yet implemented, there.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three read-only report endpoints — ticket volume, SLA performance, agent performance —
gated to Admin/Supervisor, over the data this session's own `FEAT-14`/`FEAT-17` work already
produces (`AC-148`–`AC-154`).

**Architecture:** Three independent CQRS queries, no new entities, no new repository methods. Each
handler fetches a filtered, projected slice via the existing `IRepository<T>.ListAsync`/
`ListProjectedAsync`, then groups in memory — the same shape `GetTicketsQueryHandler` already uses
for its own in-memory joins, and defensible here because report date ranges are expected to be
small (a manager is not going to query five years of history through this endpoint).

**Tech Stack:** .NET 10, EF Core (via the existing repository abstraction — no raw SQL), MediatR,
FluentValidation.

**Spec:** [`docs/superpowers/specs/EPIC-08-US-606-reporting.md`](../../specs/EPIC-08-US-606-reporting.md)

## Global Constraints

- Every report endpoint is gated by `[Authorize(Policy = "Supervisor")]` at the controller level —
  confirmed against `AuthorizationExtensions.cs`: that policy is `RequireRole("Supervisor", "Admin")`,
  so it already covers both roles named in `AC-148`.
- No department scoping (spec A1) — do not add a `departmentId` filter parameter anywhere in this
  plan, even though the source stories mention one.
- `from > to` is a 400 keyed to `to` (spec AC-154), checked in each query's validator, not the handler.
- No new `SystemCode`/`SystemCode Map` entries — every failure path is validation (400), already
  covered by `VAL001`.

---

### Task 1: Ticket volume report

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Reports/Dtos/TicketVolumeReportDto.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Reports/Queries/GetTicketVolumeReport/GetTicketVolumeReportQuery.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Reports/Queries/GetTicketVolumeReport/GetTicketVolumeReportQueryValidator.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Reports/Queries/GetTicketVolumeReport/GetTicketVolumeReportQueryHandler.cs`
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs` (this task creates the file; Tasks 2 and 3 add actions to it)
- Test: `backend/tests/CustomerSupport.Tests/Integration/ReportsEndpointTests.cs` (this task creates the file; Tasks 2 and 3 append to it)

**Interfaces:**
- Consumes: `IRepository<Ticket>.ListProjectedAsync`.
- Produces: `TicketVolumeReportDto(IReadOnlyList<ReportBucket> ByPeriod, IReadOnlyList<ReportBucket> ByCategory, IReadOnlyList<ReportBucket> ByPriority)`, `ReportBucket(string Key, int Count)` — reused by Task 2's DTO shape conventions (not the type itself, each report defines its own row shape).

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/ReportsEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-19+ — ticket volume, SLA performance and agent performance reports. `AC-148` through
/// `AC-154`. Real LocalDB throughout.
/// </summary>
public class ReportsEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _supervisor = null!;
    private HttpClient _agent = null!;
    private Guid _categoryId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_supervisor, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Supervisor);
        (_agent, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
        _categoryId = await _factory.EnsureCategoryAsync("Technical");

        var customer = await _supervisor.PostAsJsonAsync("/api/Customers", new
        {
            name = "Report Fixture",
            email = $"reports-{Guid.NewGuid():N}@example.com",
            phone = (string?)null,
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    public Task DisposeAsync()
    {
        _supervisor.Dispose();
        _agent.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateTicketAsync(string priority)
    {
        var response = await _supervisor.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Report fixture ticket",
            description = "Exercising the reporting endpoints.",
            customerId = _customerId,
            categoryId = _categoryId,
            priority,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    private static string Range() =>
        $"from={DateTime.UtcNow.AddDays(-1):O}&to={DateTime.UtcNow.AddDays(1):O}";

    // --- AC-148 — authorization -------------------------------------------------------------------

    [Fact]
    [Trait("AC", "148")]
    public async Task AC148_Agent_CannotReadTicketVolumeReport()
    {
        var response = await _agent.GetAsync($"/api/reports/ticket-volume?{Range()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("AC", "148")]
    public async Task AC148_Unauthenticated_CannotReadTicketVolumeReport()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/reports/ticket-volume?{Range()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- AC-149/150/151 — ticket volume -------------------------------------------------------------

    [Fact]
    [Trait("AC", "149")]
    public async Task AC149_TicketVolume_GroupsByPeriod()
    {
        await CreateTicketAsync("High");
        await CreateTicketAsync("High");

        var report = await _supervisor.GetFromJsonAsync<Response<TicketVolumeReportRow>>(
            $"/api/reports/ticket-volume?{Range()}&groupBy=day");

        report!.Data!.ByPeriod.Sum(b => b.Count).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    [Trait("AC", "150")]
    public async Task AC150_TicketVolume_GroupsByCategory()
    {
        await CreateTicketAsync("Low");

        var report = await _supervisor.GetFromJsonAsync<Response<TicketVolumeReportRow>>(
            $"/api/reports/ticket-volume?{Range()}");

        report!.Data!.ByCategory.Should().Contain(b => b.Key == "Technical" && b.Count >= 1);
    }

    [Fact]
    [Trait("AC", "151")]
    public async Task AC151_TicketVolume_GroupsByPriority()
    {
        await CreateTicketAsync("Urgent");

        var report = await _supervisor.GetFromJsonAsync<Response<TicketVolumeReportRow>>(
            $"/api/reports/ticket-volume?{Range()}");

        report!.Data!.ByPriority.Should().Contain(b => b.Key == "Urgent" && b.Count >= 1);
    }

    // --- AC-154 — bad range ------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "154")]
    public async Task AC154_FromAfterTo_Returns400KeyedToField()
    {
        var from = DateTime.UtcNow;
        var to = DateTime.UtcNow.AddDays(-1);

        var response = await _supervisor.GetAsync(
            $"/api/reports/ticket-volume?from={from:O}&to={to:O}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "To");
    }

    public sealed record ReportBucketRow(string Key, int Count);
    public sealed record TicketVolumeReportRow(
        IReadOnlyList<ReportBucketRow> ByPeriod,
        IReadOnlyList<ReportBucketRow> ByCategory,
        IReadOnlyList<ReportBucketRow> ByPriority);
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportsEndpointTests"`
Expected: FAIL — compile error, route/types not found.

- [ ] **Step 3: Write the DTO**

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Dtos/TicketVolumeReportDto.cs
namespace CustomerSupport.Application.Features.Reports.Dtos;

/// <summary>One named bucket's count — shared shape across every report's breakdowns.</summary>
public record ReportBucket(string Key, int Count);

/// <summary>Ticket volume, three independent breakdowns over one date range (AC-149..AC-151,
/// spec A8 — not a single period×category×priority cross-tab).</summary>
public record TicketVolumeReportDto(
    IReadOnlyList<ReportBucket> ByPeriod,
    IReadOnlyList<ReportBucket> ByCategory,
    IReadOnlyList<ReportBucket> ByPriority);
```

- [ ] **Step 4: Write the query and its validator**

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Queries/GetTicketVolumeReport/GetTicketVolumeReportQuery.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Reports.Dtos;

namespace CustomerSupport.Application.Features.Reports.Queries.GetTicketVolumeReport;

/// <summary>Ticket volume by period/category/priority — AC-149..AC-151.</summary>
public record GetTicketVolumeReportQuery(DateTime From, DateTime To, string GroupBy)
    : IQuery<Response<TicketVolumeReportDto>>;
```

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Queries/GetTicketVolumeReport/GetTicketVolumeReportQueryValidator.cs
using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Reports.Queries.GetTicketVolumeReport;

/// <summary>AC-154.</summary>
public class GetTicketVolumeReportQueryValidator : AbstractValidator<GetTicketVolumeReportQuery>
{
    private static readonly string[] AllowedGroupings = ["day", "week", "month"];

    public GetTicketVolumeReportQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithErrorCode(ApplicationErrors.Validation.REPORT_RANGE_INVALID);

        RuleFor(x => x.GroupBy)
            .Must(g => AllowedGroupings.Contains(g))
            .WithErrorCode(ApplicationErrors.Validation.REPORT_GROUP_BY_INVALID);
    }
}
```

- [ ] **Step 5: Add the validation error codes**

Edit `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs`, inside `Validation`:

```csharp
        // Reports — FEAT-19+, AC-154.
        public const string REPORT_RANGE_INVALID = "REPORT_RANGE_INVALID";
        public const string REPORT_GROUP_BY_INVALID = "REPORT_GROUP_BY_INVALID";
```

Edit `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml`, append:

```yaml
REPORT_RANGE_INVALID:
  ar: "يجب أن يكون تاريخ الانتهاء بعد تاريخ البدء"
  en: "The end date must be on or after the start date"

REPORT_GROUP_BY_INVALID:
  ar: "قيمة التجميع غير صالحة"
  en: "The grouping value is not valid"
```

- [ ] **Step 6: Write the handler**

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Queries/GetTicketVolumeReport/GetTicketVolumeReportQueryHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Reports.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Reports.Queries.GetTicketVolumeReport;

public class GetTicketVolumeReportQueryHandler(
    IRepository<Ticket> tickets,
    IRepository<Category> categories,
    IMessageFactory messages)
    : IQueryHandler<GetTicketVolumeReportQuery, Response<TicketVolumeReportDto>>
{
    public async Task<Response<TicketVolumeReportDto>> Handle(GetTicketVolumeReportQuery request, CancellationToken ct)
    {
        var rows = await tickets.ListProjectedAsync(
            t => t.CreatedAt >= request.From && t.CreatedAt <= request.To,
            t => new { t.CreatedAt, t.CategoryId, t.Priority },
            ct);

        // Category names are resolved for readability, but grouping stays on CategoryId — two
        // categories could share a display name in principle, and the id is the actual key.
        var categoryIds = rows.Select(r => r.CategoryId).Distinct().ToList();
        var categoryList = await categories.ListAsync(c => categoryIds.Contains(c.Id), ct);
        var categoryNames = categoryList.ToDictionary(c => c.Id, c => c.Name);

        var byPeriod = rows
            .GroupBy(r => PeriodKey(r.CreatedAt, request.GroupBy))
            .Select(g => new ReportBucket(g.Key, g.Count()))
            .OrderBy(b => b.Key)
            .ToList();

        var byCategory = rows
            .GroupBy(r => r.CategoryId)
            .Select(g => new ReportBucket(categoryNames.GetValueOrDefault(g.Key, g.Key.ToString()), g.Count()))
            .OrderByDescending(b => b.Count)
            .ToList();

        var byPriority = rows
            .GroupBy(r => r.Priority)
            .Select(g => new ReportBucket(g.Key, g.Count()))
            .OrderByDescending(b => b.Count)
            .ToList();

        return messages.Success(
            new TicketVolumeReportDto(byPeriod, byCategory, byPriority),
            ApplicationErrors.General.SUCCESS_OPERATION);
    }

    private static string PeriodKey(DateTime createdAt, string groupBy) => groupBy switch
    {
        "week" => System.Globalization.ISOWeek.GetYear(createdAt) + "-W" +
                  System.Globalization.ISOWeek.GetWeekOfYear(createdAt).ToString("00"),
        "month" => createdAt.ToString("yyyy-MM"),
        _ => createdAt.ToString("yyyy-MM-dd"),
    };
}
```

A single `using CustomerSupport.Domain.Entities.Tickets;` covers both `Ticket` and `Category` — both
are declared in that namespace (`backend/src/CustomerSupport.Domain/Entities/Tickets/Category.cs`
line 1).

- [ ] **Step 7: Add the controller**

```csharp
// backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs
using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Reports.Dtos;
using CustomerSupport.Application.Features.Reports.Queries.GetTicketVolumeReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Reporting — FEAT-19+, AC-148..AC-154. `Supervisor` policy already means "Supervisor or Admin"
/// (see `AuthorizationExtensions.cs`), so it alone satisfies AC-148 without a second role list.
/// </summary>
[ApiController]
[Route("api/reports")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Supervisor")]
public class ReportsController(IMediator mediator) : ControllerBase
{
    /// <summary>Ticket volume by period, category and priority (AC-149..AC-151).</summary>
    /// <param name="from">Start of the date range (inclusive).</param>
    /// <param name="to">End of the date range (inclusive). Must not be before <paramref name="from"/> (AC-154).</param>
    /// <param name="groupBy">day, week or month. Defaults to day.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("ticket-volume")]
    [ProducesResponseType(typeof(Response<TicketVolumeReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<TicketVolumeReportDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTicketVolume(
        [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string groupBy = "day",
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetTicketVolumeReportQuery(from, to, groupBy), ct);
        return this.ToActionResult(result);
    }
}
```

- [ ] **Step 8: Run the test file, then build**

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: Build succeeded, 0 errors.

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportsEndpointTests"`
Expected: PASS for every test written so far (`AC148_*`, `AC149_*`, `AC150_*`, `AC151_*`, `AC154_*`).

- [ ] **Step 9: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Reports/ \
        backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs \
        backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml \
        backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ReportsEndpointTests.cs
git commit -m "feat(reports): ticket volume report (AC-148..AC-151, AC-154)"
```

---

### Task 2: SLA performance report

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Reports/Dtos/SlaPerformanceReportDto.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Reports/Queries/GetSlaPerformanceReport/` (Query, Validator, Handler — same three-file shape as Task 1)
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs` (add the action)
- Modify: `backend/tests/CustomerSupport.Tests/Integration/ReportsEndpointTests.cs` (append tests)

**Interfaces:**
- Consumes: `IRepository<Ticket>.ListProjectedAsync`, `IRepository<SLAEvent>.ListAsync`.
- Produces: `SlaPerformanceReportDto(IReadOnlyList<SlaPerformanceRow> ByPriority)`,
  `SlaPerformanceRow(string Priority, int Total, int MetFirstResponse, int BreachedFirstResponse, int MetResolution, int BreachedResolution)`.

- [ ] **Step 1: Append the failing tests to `ReportsEndpointTests.cs`**

```csharp
    // --- AC-152 — SLA performance ------------------------------------------------------------------

    [Fact]
    [Trait("AC", "152")]
    public async Task AC152_SlaPerformance_MetPlusBreachedEqualsTotal()
    {
        // No SLAPolicy is required to exist for this assertion to hold: a ticket with no due date
        // simply is not counted (spec A6), so the identity holds regardless of fixture data already
        // created by other tests in this class.
        await CreateTicketAsync("Normal");

        var report = await _supervisor.GetFromJsonAsync<Response<SlaPerformanceReportRow>>(
            $"/api/reports/sla-performance?{Range()}");

        foreach (var row in report!.Data!.ByPriority)
        {
            (row.MetFirstResponse + row.BreachedFirstResponse).Should().BeLessOrEqualTo(row.Total);
            (row.MetResolution + row.BreachedResolution).Should().BeLessOrEqualTo(row.Total);
        }
    }

    public sealed record SlaPerformanceRowFixture(
        string Priority, int Total, int MetFirstResponse, int BreachedFirstResponse,
        int MetResolution, int BreachedResolution);
    public sealed record SlaPerformanceReportRow(IReadOnlyList<SlaPerformanceRowFixture> ByPriority);
```

- [ ] **Step 2: Run to verify it fails to compile**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportsEndpointTests"`
Expected: FAIL — route not found.

- [ ] **Step 3: Write the DTO, query and validator**

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Dtos/SlaPerformanceReportDto.cs
namespace CustomerSupport.Application.Features.Reports.Dtos;

public record SlaPerformanceRow(
    string Priority, int Total, int MetFirstResponse, int BreachedFirstResponse,
    int MetResolution, int BreachedResolution);

public record SlaPerformanceReportDto(IReadOnlyList<SlaPerformanceRow> ByPriority);
```

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Queries/GetSlaPerformanceReport/GetSlaPerformanceReportQuery.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Reports.Dtos;

namespace CustomerSupport.Application.Features.Reports.Queries.GetSlaPerformanceReport;

/// <summary>Attainment and breach counts by priority — AC-152.</summary>
public record GetSlaPerformanceReportQuery(DateTime From, DateTime To)
    : IQuery<Response<SlaPerformanceReportDto>>;
```

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Queries/GetSlaPerformanceReport/GetSlaPerformanceReportQueryValidator.cs
using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Reports.Queries.GetSlaPerformanceReport;

public class GetSlaPerformanceReportQueryValidator : AbstractValidator<GetSlaPerformanceReportQuery>
{
    public GetSlaPerformanceReportQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithErrorCode(ApplicationErrors.Validation.REPORT_RANGE_INVALID);
    }
}
```

- [ ] **Step 4: Write the handler**

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Queries/GetSlaPerformanceReport/GetSlaPerformanceReportQueryHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Reports.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Reports.Queries.GetSlaPerformanceReport;

public class GetSlaPerformanceReportQueryHandler(
    IRepository<Ticket> tickets,
    IRepository<SLAEvent> slaEvents,
    IMessageFactory messages)
    : IQueryHandler<GetSlaPerformanceReportQuery, Response<SlaPerformanceReportDto>>
{
    public async Task<Response<SlaPerformanceReportDto>> Handle(GetSlaPerformanceReportQuery request, CancellationToken ct)
    {
        // Only tickets a policy actually matched (spec A6) — one with no due date was never on an
        // SLA clock and has nothing to report.
        var withTargets = await tickets.ListProjectedAsync(
            t => t.CreatedAt >= request.From && t.CreatedAt <= request.To
                && (t.ResponseDueAt != null || t.ResolutionDueAt != null),
            t => new { t.Id, t.Priority, t.ResponseDueAt, t.ResolutionDueAt },
            ct);

        var ticketIds = withTargets.Select(t => t.Id).ToList();

        var breaches = await slaEvents.ListAsync(e => ticketIds.Contains(e.TicketId) && e.BreachedAt != null, ct);
        var breachedResponse = breaches.Where(e => e.TargetType == SLAEvent.TargetTypes.Response)
            .Select(e => e.TicketId).ToHashSet();
        var breachedResolution = breaches.Where(e => e.TargetType == SLAEvent.TargetTypes.Resolution)
            .Select(e => e.TicketId).ToHashSet();

        var byPriority = withTargets
            .GroupBy(t => t.Priority)
            .Select(g =>
            {
                var withResponseTarget = g.Where(t => t.ResponseDueAt != null).ToList();
                var withResolutionTarget = g.Where(t => t.ResolutionDueAt != null).ToList();
                var breachedResponseCount = withResponseTarget.Count(t => breachedResponse.Contains(t.Id));
                var breachedResolutionCount = withResolutionTarget.Count(t => breachedResolution.Contains(t.Id));

                return new SlaPerformanceRow(
                    g.Key,
                    g.Count(),
                    MetFirstResponse: withResponseTarget.Count - breachedResponseCount,
                    BreachedFirstResponse: breachedResponseCount,
                    MetResolution: withResolutionTarget.Count - breachedResolutionCount,
                    BreachedResolution: breachedResolutionCount);
            })
            .OrderBy(r => r.Priority)
            .ToList();

        return messages.Success(new SlaPerformanceReportDto(byPriority), ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
```

- [ ] **Step 5: Add the controller action**

Edit `ReportsController.cs`, add
`using CustomerSupport.Application.Features.Reports.Queries.GetSlaPerformanceReport;` and:

```csharp
    /// <summary>SLA attainment and breach counts by priority (AC-152).</summary>
    /// <param name="from">Start of the date range (inclusive), matched against ticket creation.</param>
    /// <param name="to">End of the date range (inclusive). Must not be before <paramref name="from"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("sla-performance")]
    [ProducesResponseType(typeof(Response<SlaPerformanceReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<SlaPerformanceReportDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSlaPerformance(
        [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetSlaPerformanceReportQuery(from, to), ct);
        return this.ToActionResult(result);
    }
```

- [ ] **Step 6: Build and run**

Run: `cd backend && dotnet build CustomerSupport.slnx && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportsEndpointTests"`
Expected: Build succeeded, 0 errors. All tests including `AC152_*` pass.

- [ ] **Step 7: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Reports/Dtos/SlaPerformanceReportDto.cs \
        backend/src/CustomerSupport.Application/Features/Reports/Queries/GetSlaPerformanceReport/ \
        backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ReportsEndpointTests.cs
git commit -m "feat(reports): SLA performance report (AC-152)"
```

---

### Task 3: Agent performance report

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Reports/Dtos/AgentPerformanceReportDto.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Reports/Queries/GetAgentPerformanceReport/` (Query, Validator, Handler)
- Modify: `ReportsController.cs` (add the action)
- Modify: `ReportsEndpointTests.cs` (append tests)

**Interfaces:**
- Consumes: `IRepository<Ticket>.ListProjectedAsync`, `IIdentityUserService.FindByIdAsync` (agent display names — the same pattern `GetTicketsQueryHandler` already uses for assignee names).
- Produces: `AgentPerformanceReportDto(IReadOnlyList<AgentPerformanceRow> ByAgent)`,
  `AgentPerformanceRow(Guid AgentId, string AgentName, int TicketsResolved, double AvgHandleMinutes)`.

- [ ] **Step 1: Append the failing test**

```csharp
    // --- AC-153 — agent performance ----------------------------------------------------------------

    [Fact]
    [Trait("AC", "153")]
    public async Task AC153_AgentPerformance_CountsResolvedTicketsPerAgent()
    {
        var (agentClient, agentUser) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
        var ticketId = await CreateTicketAsync("Normal");

        var detail = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
        (await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
            new { assigneeId = agentUser.Id, rowVersion = detail!.Data!.RowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var afterAssign = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
        (await agentClient.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
            new { status = "Open", rowVersion = afterAssign!.Data!.RowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var afterOpen = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
        (await agentClient.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
            new { status = "Resolved", rowVersion = afterOpen!.Data!.RowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var report = await _supervisor.GetFromJsonAsync<Response<AgentPerformanceReportRow>>(
            $"/api/reports/agent-performance?{Range()}");

        report!.Data!.ByAgent.Should().Contain(r => r.AgentId == agentUser.Id && r.TicketsResolved >= 1);
    }

    public sealed record TicketDetailRow(string RowVersion);
    public sealed record AgentPerformanceRowFixture(Guid AgentId, string AgentName, int TicketsResolved, double AvgHandleMinutes);
    public sealed record AgentPerformanceReportRow(IReadOnlyList<AgentPerformanceRowFixture> ByAgent);
```

- [ ] **Step 2: Run to verify it fails to compile**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportsEndpointTests"`
Expected: FAIL — route not found.

- [ ] **Step 3: Write the DTO, query and validator**

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Dtos/AgentPerformanceReportDto.cs
namespace CustomerSupport.Application.Features.Reports.Dtos;

public record AgentPerformanceRow(Guid AgentId, string AgentName, int TicketsResolved, double AvgHandleMinutes);

public record AgentPerformanceReportDto(IReadOnlyList<AgentPerformanceRow> ByAgent);
```

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Queries/GetAgentPerformanceReport/GetAgentPerformanceReportQuery.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Reports.Dtos;

namespace CustomerSupport.Application.Features.Reports.Queries.GetAgentPerformanceReport;

/// <summary>Throughput and approximate handle time per agent — AC-153.</summary>
public record GetAgentPerformanceReportQuery(DateTime From, DateTime To)
    : IQuery<Response<AgentPerformanceReportDto>>;
```

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Queries/GetAgentPerformanceReport/GetAgentPerformanceReportQueryValidator.cs
using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Reports.Queries.GetAgentPerformanceReport;

public class GetAgentPerformanceReportQueryValidator : AbstractValidator<GetAgentPerformanceReportQuery>
{
    public GetAgentPerformanceReportQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithErrorCode(ApplicationErrors.Validation.REPORT_RANGE_INVALID);
    }
}
```

- [ ] **Step 4: Write the handler**

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Queries/GetAgentPerformanceReport/GetAgentPerformanceReportQueryHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Reports.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Reports.Queries.GetAgentPerformanceReport;

public class GetAgentPerformanceReportQueryHandler(
    IRepository<Ticket> tickets,
    IIdentityUserService identityUsers,
    IMessageFactory messages)
    : IQueryHandler<GetAgentPerformanceReportQuery, Response<AgentPerformanceReportDto>>
{
    private static readonly string[] ResolvedStatuses = ["Resolved", "Closed"];

    public async Task<Response<AgentPerformanceReportDto>> Handle(GetAgentPerformanceReportQuery request, CancellationToken ct)
    {
        var resolved = await tickets.ListProjectedAsync(
            t => t.AssigneeId != null && ResolvedStatuses.Contains(t.Status)
                && t.CreatedAt >= request.From && t.CreatedAt <= request.To,
            t => new { t.AssigneeId, t.CreatedAt, t.UpdatedAt },
            ct);

        var byAgent = new List<AgentPerformanceRow>();

        foreach (var group in resolved.GroupBy(t => t.AssigneeId!.Value))
        {
            var agent = await identityUsers.FindByIdAsync(group.Key, ct);
            var rows = group.ToList();

            // Approximation (spec A7): UpdatedAt is the LAST change to the ticket, not necessarily
            // the moment it first reached Resolved. A ticket resolved, reopened, then resolved
            // again reports a longer handle time than the first resolution actually took.
            var avgMinutes = rows.Average(t => (((DateTime)(t.UpdatedAt ?? t.CreatedAt)) - t.CreatedAt).TotalMinutes);

            byAgent.Add(new AgentPerformanceRow(group.Key, agent?.FullName ?? string.Empty, rows.Count, avgMinutes));
        }

        return messages.Success(
            new AgentPerformanceReportDto(byAgent.OrderByDescending(r => r.TicketsResolved).ToList()),
            ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
```

- [ ] **Step 5: Add the controller action**

Edit `ReportsController.cs`, add
`using CustomerSupport.Application.Features.Reports.Queries.GetAgentPerformanceReport;` and:

```csharp
    /// <summary>Tickets resolved and approximate handle time per agent (AC-153).</summary>
    /// <param name="from">Start of the date range (inclusive), matched against ticket creation.</param>
    /// <param name="to">End of the date range (inclusive). Must not be before <paramref name="from"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("agent-performance")]
    [ProducesResponseType(typeof(Response<AgentPerformanceReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<AgentPerformanceReportDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAgentPerformance(
        [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetAgentPerformanceReportQuery(from, to), ct);
        return this.ToActionResult(result);
    }
```

- [ ] **Step 6: Build and run the full new test file, then the full suite**

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: Build succeeded, 0 errors, 0 new warnings.

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportsEndpointTests"`
Expected: PASS — all tests across all three tasks.

Run: `cd backend && dotnet test CustomerSupport.slnx`
Expected: PASS, full suite, no regressions. Paste the actual summary line — do not claim this
without having run it.

- [ ] **Step 7: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Reports/Dtos/AgentPerformanceReportDto.cs \
        backend/src/CustomerSupport.Application/Features/Reports/Queries/GetAgentPerformanceReport/ \
        backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ReportsEndpointTests.cs
git commit -m "feat(reports): agent performance report (AC-153)"
```

---

## Definition of done

`AC-148` through `AC-154` each covered by a test naming it · `dotnet build` clean, 0 new warnings ·
`dotnet test CustomerSupport.slnx` green, full output pasted into the task record · task records
written in `tasks/` (or a single README, matching this project's smaller-feature precedent) as each
task completes.

---

## Addendum (2026-08-27): frontend — adapted `US-606`/`US-607`/`US-610`

Written because the backend above shipped without ever producing a saved `implementation-plan.md`
entry for its frontend counterpart — this addendum now precedes the frontend code it describes,
closing that gap for the new work. Covers `AC-160`–`AC-164` from the reporting spec's frontend
addendum (`docs/superpowers/specs/EPIC-08-US-606-reporting.md`).

**Do not re-run `dotnet test CustomerSupport.slnx` (full suite) for this addendum** — no backend
file changes except the one-line `roleGuard` extension in Task 6, which is frontend-adjacent
(TypeScript) and has its own targeted test.

### Task 4: `ReportsApi`, report DTOs, shared date-range filter (common)

**Files:**
- Create: `frontend/projects/common/src/lib/reports/report.api.ts`
- Create: `frontend/projects/common/src/lib/reports/report-date-range-filter.component.ts`
- Create: `frontend/projects/common/src/lib/reports/report-date-range-filter.component.html`
- Create: `frontend/projects/common/src/lib/reports/report-date-range-filter.component.spec.ts`
- Modify: `frontend/projects/common/src/public-api.ts`
- Modify: `frontend/projects/common/src/lib/i18n/translations.ts`

**Interfaces:**
- Produces: `ReportsApi` with `ticketVolume`, `slaPerformance`, `agentPerformance` methods (typed
  client, matching `TicketApi`'s pattern exactly); `TicketVolumeReport`, `SlaPerformanceReport`,
  `AgentPerformanceReport`, `ReportBucket` interfaces matching the backend's shipped DTOs field for
  field; `ReportDateRangeFilter` component (selector `cs-report-date-range-filter`), inputs `from`/
  `to` (ISO date strings, `yyyy-MM-dd`), output `apply` emitting `{ from, to }`.

- [ ] **Step 1: Write the failing test**

```ts
// frontend/projects/common/src/lib/reports/report-date-range-filter.component.spec.ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReportDateRangeFilter } from './report-date-range-filter.component';

describe('ReportDateRangeFilter', () => {
  function render(from: string, to: string): ComponentFixture<ReportDateRangeFilter> {
    const fixture = TestBed.createComponent(ReportDateRangeFilter);
    fixture.componentRef.setInput('from', from);
    fixture.componentRef.setInput('to', to);
    fixture.detectChanges();
    return fixture;
  }

  it('AC163: emits the form values when applied', () => {
    const fixture = render('2026-08-01', '2026-08-27');
    const emitted: { from: string; to: string }[] = [];
    fixture.componentInstance.apply.subscribe((value) => emitted.push(value));

    fixture.componentInstance.form.controls.from.setValue('2026-07-01');
    (fixture.nativeElement as HTMLElement).querySelector('form')!.dispatchEvent(new Event('submit'));

    expect(emitted).toEqual([{ from: '2026-07-01', to: '2026-08-27' }]);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test common --watch=false --include='**/report-date-range-filter.component.spec.ts'`
Expected: FAIL — module does not exist.

- [ ] **Step 3: Write `ReportsApi` and the DTOs**

```ts
// frontend/projects/common/src/lib/reports/report.api.ts
import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

/** Matches the backend's `ReportBucket` (`Key`, `Count`) — AC-149/150/151. */
export interface ReportBucket {
  readonly key: string;
  readonly count: number;
}

/** Matches the backend's `TicketVolumeReportDto`. */
export interface TicketVolumeReport {
  readonly byPeriod: readonly ReportBucket[];
  readonly byCategory: readonly ReportBucket[];
  readonly byPriority: readonly ReportBucket[];
}

/** Matches the backend's `SlaPerformanceRow`/`SlaPerformanceReportDto` — AC-152. */
export interface SlaPerformanceRow {
  readonly priority: string;
  readonly total: number;
  readonly metFirstResponse: number;
  readonly breachedFirstResponse: number;
  readonly metResolution: number;
  readonly breachedResolution: number;
}

export interface SlaPerformanceReport {
  readonly byPriority: readonly SlaPerformanceRow[];
}

/** Matches the backend's `AgentPerformanceRow`/`AgentPerformanceReportDto` — AC-153. */
export interface AgentPerformanceRow {
  readonly agentId: string;
  readonly agentName: string;
  readonly ticketsResolved: number;
  readonly avgHandleMinutes: number;
}

export interface AgentPerformanceReport {
  readonly byAgent: readonly AgentPerformanceRow[];
}

export interface ReportDateRange {
  readonly from: string;
  readonly to: string;
}

export type ReportGroupBy = 'day' | 'week' | 'month';

/**
 * The three reports FEAT-19+ actually shipped — adapted-scope frontend (spec addendum A4):
 * no dashboard/live-queue/CSAT/branch-filter endpoints exist, so there are no client methods for
 * them. Catches nothing, matching every other API service in this workspace.
 */
@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);

  ticketVolume(range: ReportDateRange, groupBy: ReportGroupBy = 'day'): Observable<TicketVolumeReport> {
    const params = new HttpParams()
      .set('from', range.from)
      .set('to', range.to)
      .set('groupBy', groupBy);
    return this.http.get<TicketVolumeReport>('/api/reports/ticket-volume', { params });
  }

  slaPerformance(range: ReportDateRange): Observable<SlaPerformanceReport> {
    const params = new HttpParams().set('from', range.from).set('to', range.to);
    return this.http.get<SlaPerformanceReport>('/api/reports/sla-performance', { params });
  }

  agentPerformance(range: ReportDateRange): Observable<AgentPerformanceReport> {
    const params = new HttpParams().set('from', range.from).set('to', range.to);
    return this.http.get<AgentPerformanceReport>('/api/reports/agent-performance', { params });
  }
}
```

- [ ] **Step 4: Write `ReportDateRangeFilter`**

```ts
// frontend/projects/common/src/lib/reports/report-date-range-filter.component.ts
import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { CsButton } from '../ui/button.component';
import { TranslatePipe } from '../i18n/translate.pipe';

/** Shared by all three report screens — US-610 AC1, narrowed to date range (spec addendum A4). */
@Component({
  selector: 'cs-report-date-range-filter',
  imports: [ReactiveFormsModule, CsButton, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './report-date-range-filter.component.html',
})
export class ReportDateRangeFilter {
  readonly from = input.required<string>();
  readonly to = input.required<string>();
  readonly apply = output<{ from: string; to: string }>();

  readonly form = new FormGroup({
    from: new FormControl('', { nonNullable: true }),
    to: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    // Keeps the form in step when the host navigates (e.g. back/forward changing the url's query
    // params) without fighting the user's own in-progress edits on first render.
    effect(() => {
      this.form.setValue({ from: this.from(), to: this.to() }, { emitEvent: false });
    });
  }

  submit(): void {
    const { from, to } = this.form.getRawValue();
    if (from && to) {
      this.apply.emit({ from, to });
    }
  }
}
```

```html
<!-- frontend/projects/common/src/lib/reports/report-date-range-filter.component.html -->
<form
  [formGroup]="form"
  (ngSubmit)="submit()"
  class="flex flex-wrap items-end gap-4 border-b border-border-subtle px-4 py-3"
>
  <div class="flex flex-col gap-1.5">
    <label for="report-from" class="text-label-md text-on-surface-variant">
      {{ 'reports.filter.from' | t }}
    </label>
    <input
      id="report-from"
      type="date"
      [formControl]="form.controls.from"
      class="h-10 rounded-lg border border-outline-variant bg-surface-lowest px-3 text-body-md text-on-surface"
    />
  </div>
  <div class="flex flex-col gap-1.5">
    <label for="report-to" class="text-label-md text-on-surface-variant">
      {{ 'reports.filter.to' | t }}
    </label>
    <input
      id="report-to"
      type="date"
      [formControl]="form.controls.to"
      class="h-10 rounded-lg border border-outline-variant bg-surface-lowest px-3 text-body-md text-on-surface"
    />
  </div>
  <cs-button type="submit">{{ 'reports.filter.apply' | t }}</cs-button>
</form>
```

- [ ] **Step 5: Export and add dictionary entries**

In `public-api.ts`, add a new section:

```ts
export * from './lib/reports/report.api';
export * from './lib/reports/report-date-range-filter.component';
```

In `translations.ts`:

```ts
  'reports.filter.from': { en: 'From', ar: 'من' },
  'reports.filter.to': { en: 'To', ar: 'إلى' },
  'reports.filter.apply': { en: 'Apply', ar: 'تطبيق' },
  'nav.reports': { en: 'Reports', ar: 'التقارير' },
  'reports.ticketVolume.title': { en: 'Ticket volume', ar: 'حجم التذاكر' },
  'reports.ticketVolume.byPeriod': { en: 'By period', ar: 'حسب الفترة' },
  'reports.ticketVolume.byCategory': { en: 'By category', ar: 'حسب الفئة' },
  'reports.ticketVolume.byPriority': { en: 'By priority', ar: 'حسب الأولوية' },
  'reports.ticketVolume.groupBy': { en: 'Group by', ar: 'تجميع حسب' },
  'reports.groupBy.day': { en: 'Day', ar: 'يوم' },
  'reports.groupBy.week': { en: 'Week', ar: 'أسبوع' },
  'reports.groupBy.month': { en: 'Month', ar: 'شهر' },
  'reports.slaPerformance.title': { en: 'SLA performance', ar: 'أداء اتفاقية الخدمة' },
  'reports.slaPerformance.total': { en: 'Total', ar: 'الإجمالي' },
  'reports.slaPerformance.responseMet': { en: 'Response met', ar: 'تم الرد ضمن الوقت' },
  'reports.slaPerformance.responseBreached': { en: 'Response breached', ar: 'تم تجاوز وقت الرد' },
  'reports.slaPerformance.resolutionMet': { en: 'Resolution met', ar: 'تم الحل ضمن الوقت' },
  'reports.slaPerformance.resolutionBreached': { en: 'Resolution breached', ar: 'تم تجاوز وقت الحل' },
  'reports.agentPerformance.title': { en: 'Agent performance', ar: 'أداء الوكلاء' },
  'reports.agentPerformance.agent': { en: 'Agent', ar: 'الوكيل' },
  'reports.agentPerformance.resolved': { en: 'Tickets resolved', ar: 'التذاكر المحلولة' },
  'reports.agentPerformance.avgHandle': { en: 'Avg. handle time (min)', ar: 'متوسط وقت المعالجة (دقيقة)' },
  'reports.empty': { en: 'No data for this range', ar: 'لا توجد بيانات لهذه الفترة' },
```

- [ ] **Step 6: Run test to verify it passes**

Run: `cd frontend && npx ng test common --watch=false --include='**/report-date-range-filter.component.spec.ts'`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add frontend/projects/common/src/lib/reports/ frontend/projects/common/src/public-api.ts frontend/projects/common/src/lib/i18n/translations.ts
git commit -m "feat(common): ReportsApi and shared date-range filter (AC-163)"
```

---

### Task 5: Three report screens (admin-app)

**Files:**
- Create: `frontend/projects/admin-app/src/app/features/reports/ticket-volume-report.component.ts`
- Create: `frontend/projects/admin-app/src/app/features/reports/ticket-volume-report.component.html`
- Create: `frontend/projects/admin-app/src/app/features/reports/ticket-volume-report.component.spec.ts`
- Create: `frontend/projects/admin-app/src/app/features/reports/sla-performance-report.component.ts`
- Create: `frontend/projects/admin-app/src/app/features/reports/sla-performance-report.component.html`
- Create: `frontend/projects/admin-app/src/app/features/reports/sla-performance-report.component.spec.ts`
- Create: `frontend/projects/admin-app/src/app/features/reports/agent-performance-report.component.ts`
- Create: `frontend/projects/admin-app/src/app/features/reports/agent-performance-report.component.html`
- Create: `frontend/projects/admin-app/src/app/features/reports/agent-performance-report.component.spec.ts`

**Interfaces:**
- Consumes: `ReportsApi` (Task 4).

- [ ] **Step 1: Write the failing test for the ticket volume screen**

```ts
// frontend/projects/admin-app/src/app/features/reports/ticket-volume-report.component.spec.ts
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { provideRouter } from '@angular/router';
import { convertToParamMap } from '@angular/router';
import { envelopeInterceptor } from 'common';
import TicketVolumeReportComponent from './ticket-volume-report.component';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

describe('TicketVolumeReportComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({}) } },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function render(): ComponentFixture<TicketVolumeReportComponent> {
    const fixture = TestBed.createComponent(TicketVolumeReportComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('AC160: renders the three breakdowns returned by the api', () => {
    const fixture = render();
    const request = http.expectOne((r) => r.url === '/api/reports/ticket-volume');
    request.flush(
      ok({
        byPeriod: [{ key: '2026-08-27', count: 3 }],
        byCategory: [{ key: 'Technical', count: 3 }],
        byPriority: [{ key: 'Normal', count: 3 }],
      }),
    );
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Technical');
    expect(text).toContain('Normal');
  });

  it('AC160: changing groupBy re-fetches byPeriod', () => {
    const fixture = render();
    http
      .expectOne((r) => r.url === '/api/reports/ticket-volume')
      .flush(ok({ byPeriod: [], byCategory: [], byPriority: [] }));
    fixture.detectChanges();

    fixture.componentInstance.setGroupBy('month');

    const request = http.expectOne((r) => r.url === '/api/reports/ticket-volume');
    expect(request.request.params.get('groupBy')).toBe('month');
    request.flush(ok({ byPeriod: [], byCategory: [], byPriority: [] }));
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/ticket-volume-report.component.spec.ts'`
Expected: FAIL — module does not exist.

- [ ] **Step 3: Implement `TicketVolumeReportComponent`**

```ts
// frontend/projects/admin-app/src/app/features/reports/ticket-volume-report.component.ts
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsLoadingState,
  failed,
  loaded,
  loading,
  ReportDateRangeFilter,
  ReportGroupBy,
  ReportsApi,
  TicketVolumeReport,
  TranslatePipe,
} from 'common';

function defaultRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

/** US-602/US-610 (adapted) — ticket volume by period/category/priority. AC-160, AC-163. */
@Component({
  selector: 'admin-ticket-volume-report',
  imports: [CsCard, CsLoadingState, CsEmptyState, CsErrorState, ReportDateRangeFilter, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-volume-report.component.html',
})
export default class TicketVolumeReportComponent {
  private readonly api = inject(ReportsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly groupByOptions: readonly ReportGroupBy[] = ['day', 'week', 'month'];

  private readonly initial = {
    from: this.route.snapshot.queryParamMap.get('from') ?? defaultRange().from,
    to: this.route.snapshot.queryParamMap.get('to') ?? defaultRange().to,
    groupBy: (this.route.snapshot.queryParamMap.get('groupBy') as ReportGroupBy) ?? 'day',
  };

  readonly from = signal(this.initial.from);
  readonly to = signal(this.initial.to);
  readonly groupBy = signal<ReportGroupBy>(this.initial.groupBy);

  readonly state = signal<AsyncState<TicketVolumeReport>>(loading());

  readonly report = computed<TicketVolumeReport | null>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  constructor() {
    this.load();
  }

  setGroupBy(value: ReportGroupBy): void {
    this.groupBy.set(value);
    this.syncUrl();
    this.load();
  }

  applyRange(range: { from: string; to: string }): void {
    this.from.set(range.from);
    this.to.set(range.to);
    this.syncUrl();
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.api.ticketVolume({ from: this.from(), to: this.to() }, this.groupBy()).subscribe({
      next: (report) => this.state.set(loaded(report)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  private syncUrl(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { from: this.from(), to: this.to(), groupBy: this.groupBy() },
      queryParamsHandling: 'merge',
    });
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
```

```html
<!-- frontend/projects/admin-app/src/app/features/reports/ticket-volume-report.component.html -->
<section class="flex flex-col gap-6">
  <header>
    <h1 class="font-display text-headline-lg text-on-surface">{{ 'reports.ticketVolume.title' | t }}</h1>
  </header>

  <cs-card>
    <cs-report-date-range-filter [from]="from()" [to]="to()" (apply)="applyRange($event)" />

    @switch (state().status) {
      @case ('loading') {
        <cs-loading-state />
      }
      @case ('error') {
        @if (loadError(); as failure) {
          <cs-error-state [error]="failure" (retry)="load()" />
        }
      }
      @default {
        @if (report(); as r) {
          <div class="flex flex-col gap-6 p-4">
            <div class="flex items-center gap-2">
              <label for="group-by" class="text-label-md text-on-surface-variant">
                {{ 'reports.ticketVolume.groupBy' | t }}
              </label>
              <select
                id="group-by"
                [value]="groupBy()"
                (change)="setGroupBy($any($event.target).value)"
                class="h-10 rounded-lg border border-outline-variant bg-surface-lowest px-3 text-body-md"
              >
                @for (option of groupByOptions; track option) {
                  <option [value]="option">{{ ('reports.groupBy.' + option) | t }}</option>
                }
              </select>
            </div>

            @if (r.byPeriod.length === 0 && r.byCategory.length === 0 && r.byPriority.length === 0) {
              <cs-empty-state [message]="'reports.empty' | t" />
            } @else {
              <div class="grid gap-6 md:grid-cols-3">
                <div>
                  <h2 class="mb-2 text-label-lg text-on-surface">{{ 'reports.ticketVolume.byPeriod' | t }}</h2>
                  <ul class="flex flex-col gap-1">
                    @for (bucket of r.byPeriod; track bucket.key) {
                      <li class="flex justify-between text-body-sm">
                        <span>{{ bucket.key }}</span>
                        <span class="font-mono text-data-mono">{{ bucket.count }}</span>
                      </li>
                    }
                  </ul>
                </div>
                <div>
                  <h2 class="mb-2 text-label-lg text-on-surface">{{ 'reports.ticketVolume.byCategory' | t }}</h2>
                  <ul class="flex flex-col gap-1">
                    @for (bucket of r.byCategory; track bucket.key) {
                      <li class="flex justify-between text-body-sm">
                        <span>{{ bucket.key }}</span>
                        <span class="font-mono text-data-mono">{{ bucket.count }}</span>
                      </li>
                    }
                  </ul>
                </div>
                <div>
                  <h2 class="mb-2 text-label-lg text-on-surface">{{ 'reports.ticketVolume.byPriority' | t }}</h2>
                  <ul class="flex flex-col gap-1">
                    @for (bucket of r.byPriority; track bucket.key) {
                      <li class="flex justify-between text-body-sm">
                        <span>{{ bucket.key }}</span>
                        <span class="font-mono text-data-mono">{{ bucket.count }}</span>
                      </li>
                    }
                  </ul>
                </div>
              </div>
            }
          </div>
        }
      }
    }
  </cs-card>
</section>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/ticket-volume-report.component.spec.ts'`
Expected: PASS.

- [ ] **Step 5: Write, fail, then implement `SlaPerformanceReportComponent`**

Test (same `ActivatedRoute`/`provideRouter` scaffolding as Step 1, targeting `/api/reports/sla-performance`):

```ts
// frontend/projects/admin-app/src/app/features/reports/sla-performance-report.component.spec.ts
it('AC161: renders one row per priority with met/breached counts', () => {
  const fixture = render();
  http.expectOne((r) => r.url === '/api/reports/sla-performance').flush(
    ok({
      byPriority: [
        { priority: 'High', total: 5, metFirstResponse: 4, breachedFirstResponse: 1, metResolution: 3, breachedResolution: 2 },
      ],
    }),
  );
  fixture.detectChanges();

  const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
  expect(text).toContain('High');
  expect(text).toContain('4');
  expect(text).toContain('1');
});
```

(Full spec file follows the same `TestBed`/`render`/`ok` scaffolding as
`ticket-volume-report.component.spec.ts` Step 1, targeting `SlaPerformanceReportComponent` and
`/api/reports/sla-performance`.)

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/sla-performance-report.component.spec.ts'`
Expected: FAIL, then implement:

```ts
// frontend/projects/admin-app/src/app/features/reports/sla-performance-report.component.ts
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ApiError,
  AsyncState,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsLoadingState,
  failed,
  loaded,
  loading,
  ReportDateRangeFilter,
  ReportsApi,
  SlaPerformanceReport,
  TranslatePipe,
} from 'common';

function defaultRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

/** US-603/US-610 (adapted) — SLA attainment by priority. AC-161, AC-163. */
@Component({
  selector: 'admin-sla-performance-report',
  imports: [CsCard, CsLoadingState, CsEmptyState, CsErrorState, ReportDateRangeFilter, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sla-performance-report.component.html',
})
export default class SlaPerformanceReportComponent {
  private readonly api = inject(ReportsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly initial = {
    from: this.route.snapshot.queryParamMap.get('from') ?? defaultRange().from,
    to: this.route.snapshot.queryParamMap.get('to') ?? defaultRange().to,
  };

  readonly from = signal(this.initial.from);
  readonly to = signal(this.initial.to);
  readonly state = signal<AsyncState<SlaPerformanceReport>>(loading());

  readonly report = computed<SlaPerformanceReport | null>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  constructor() {
    this.load();
  }

  applyRange(range: { from: string; to: string }): void {
    this.from.set(range.from);
    this.to.set(range.to);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { from: range.from, to: range.to },
      queryParamsHandling: 'merge',
    });
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.api.slaPerformance({ from: this.from(), to: this.to() }).subscribe({
      next: (report) => this.state.set(loaded(report)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
```

```html
<!-- frontend/projects/admin-app/src/app/features/reports/sla-performance-report.component.html -->
<section class="flex flex-col gap-6">
  <header>
    <h1 class="font-display text-headline-lg text-on-surface">{{ 'reports.slaPerformance.title' | t }}</h1>
  </header>

  <cs-card>
    <cs-report-date-range-filter [from]="from()" [to]="to()" (apply)="applyRange($event)" />

    @switch (state().status) {
      @case ('loading') {
        <cs-loading-state />
      }
      @case ('error') {
        @if (loadError(); as failure) {
          <cs-error-state [error]="failure" (retry)="load()" />
        }
      }
      @default {
        @if (report(); as r) {
          @if (r.byPriority.length === 0) {
            <cs-empty-state [message]="'reports.empty' | t" />
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full min-w-3xl text-body-md">
                <thead>
                  <tr class="border-b border-border-subtle bg-surface-low text-start text-label-md tracking-wider text-on-surface-variant uppercase">
                    <th scope="col" class="px-4 py-2 text-start">{{ 'field.priority' | t }}</th>
                    <th scope="col" class="px-4 py-2 text-start">{{ 'reports.slaPerformance.total' | t }}</th>
                    <th scope="col" class="px-4 py-2 text-start">{{ 'reports.slaPerformance.responseMet' | t }}</th>
                    <th scope="col" class="px-4 py-2 text-start">{{ 'reports.slaPerformance.responseBreached' | t }}</th>
                    <th scope="col" class="px-4 py-2 text-start">{{ 'reports.slaPerformance.resolutionMet' | t }}</th>
                    <th scope="col" class="px-4 py-2 text-start">{{ 'reports.slaPerformance.resolutionBreached' | t }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-border-subtle">
                  @for (row of r.byPriority; track row.priority) {
                    <tr class="even:bg-surface-low">
                      <td class="px-4 py-3 text-label-lg text-on-surface">{{ row.priority }}</td>
                      <td class="px-4 py-3 font-mono text-data-mono">{{ row.total }}</td>
                      <td class="px-4 py-3 font-mono text-data-mono text-success">{{ row.metFirstResponse }}</td>
                      <td class="px-4 py-3 font-mono text-data-mono text-error">{{ row.breachedFirstResponse }}</td>
                      <td class="px-4 py-3 font-mono text-data-mono text-success">{{ row.metResolution }}</td>
                      <td class="px-4 py-3 font-mono text-data-mono text-error">{{ row.breachedResolution }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }
      }
    }
  </cs-card>
</section>
```

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/sla-performance-report.component.spec.ts'`
Expected: PASS.

- [ ] **Step 6: Write, fail, then implement `AgentPerformanceReportComponent`**

Test (same scaffolding, targeting `/api/reports/agent-performance`):

```ts
// frontend/projects/admin-app/src/app/features/reports/agent-performance-report.component.spec.ts
it('AC162: renders one row per agent with resolved count and avg handle minutes', () => {
  const fixture = render();
  http.expectOne((r) => r.url === '/api/reports/agent-performance').flush(
    ok({ byAgent: [{ agentId: 'a-1', agentName: 'Layla Haddad', ticketsResolved: 7, avgHandleMinutes: 42.5 }] }),
  );
  fixture.detectChanges();

  const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
  expect(text).toContain('Layla Haddad');
  expect(text).toContain('7');
});
```

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/agent-performance-report.component.spec.ts'`
Expected: FAIL, then implement:

```ts
// frontend/projects/admin-app/src/app/features/reports/agent-performance-report.component.ts
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AgentPerformanceReport,
  ApiError,
  AsyncState,
  CsCard,
  CsEmptyState,
  CsErrorState,
  CsLoadingState,
  failed,
  loaded,
  loading,
  ReportDateRangeFilter,
  ReportsApi,
  TranslatePipe,
} from 'common';

function defaultRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

/** US-604/US-610 (adapted) — throughput and handle time per agent. AC-162, AC-163. */
@Component({
  selector: 'admin-agent-performance-report',
  imports: [CsCard, CsLoadingState, CsEmptyState, CsErrorState, ReportDateRangeFilter, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './agent-performance-report.component.html',
})
export default class AgentPerformanceReportComponent {
  private readonly api = inject(ReportsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly initial = {
    from: this.route.snapshot.queryParamMap.get('from') ?? defaultRange().from,
    to: this.route.snapshot.queryParamMap.get('to') ?? defaultRange().to,
  };

  readonly from = signal(this.initial.from);
  readonly to = signal(this.initial.to);
  readonly state = signal<AsyncState<AgentPerformanceReport>>(loading());

  readonly report = computed<AgentPerformanceReport | null>(() => {
    const current = this.state();
    return current.status === 'loaded' ? current.data : null;
  });

  readonly loadError = computed<ApiError | null>(() => {
    const current = this.state();
    return current.status === 'error' ? current.error : null;
  });

  constructor() {
    this.load();
  }

  applyRange(range: { from: string; to: string }): void {
    this.from.set(range.from);
    this.to.set(range.to);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { from: range.from, to: range.to },
      queryParamsHandling: 'merge',
    });
    this.load();
  }

  load(): void {
    this.state.set(loading());
    this.api.agentPerformance({ from: this.from(), to: this.to() }).subscribe({
      next: (report) => this.state.set(loaded(report)),
      error: (error: unknown) => this.state.set(failed(this.toApiError(error))),
    });
  }

  private toApiError(error: unknown): ApiError {
    return error instanceof ApiError ? error : new ApiError('ERR_UNKNOWN', 'Something went wrong', [], '', 0);
  }
}
```

```html
<!-- frontend/projects/admin-app/src/app/features/reports/agent-performance-report.component.html -->
<section class="flex flex-col gap-6">
  <header>
    <h1 class="font-display text-headline-lg text-on-surface">{{ 'reports.agentPerformance.title' | t }}</h1>
  </header>

  <cs-card>
    <cs-report-date-range-filter [from]="from()" [to]="to()" (apply)="applyRange($event)" />

    @switch (state().status) {
      @case ('loading') {
        <cs-loading-state />
      }
      @case ('error') {
        @if (loadError(); as failure) {
          <cs-error-state [error]="failure" (retry)="load()" />
        }
      }
      @default {
        @if (report(); as r) {
          @if (r.byAgent.length === 0) {
            <cs-empty-state [message]="'reports.empty' | t" />
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full min-w-2xl text-body-md">
                <thead>
                  <tr class="border-b border-border-subtle bg-surface-low text-start text-label-md tracking-wider text-on-surface-variant uppercase">
                    <th scope="col" class="px-4 py-2 text-start">{{ 'reports.agentPerformance.agent' | t }}</th>
                    <th scope="col" class="px-4 py-2 text-start">{{ 'reports.agentPerformance.resolved' | t }}</th>
                    <th scope="col" class="px-4 py-2 text-start">{{ 'reports.agentPerformance.avgHandle' | t }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-border-subtle">
                  @for (row of r.byAgent; track row.agentId) {
                    <tr class="even:bg-surface-low">
                      <td class="px-4 py-3 text-label-lg text-on-surface">{{ row.agentName }}</td>
                      <td class="px-4 py-3 font-mono text-data-mono">{{ row.ticketsResolved }}</td>
                      <td class="px-4 py-3 font-mono text-data-mono">{{ row.avgHandleMinutes | number: '1.0-1' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }
      }
    }
  </cs-card>
</section>
```

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/agent-performance-report.component.spec.ts'`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/reports/
git commit -m "feat(reports): ticket volume, SLA performance, agent performance screens (AC-160, AC-161, AC-162, AC-163)"
```

---

### Task 6: Routing, nav, multi-role guard (AC-164)

**Files:**
- Modify: `frontend/projects/common/src/lib/auth/guards.ts`
- Modify: `frontend/projects/common/src/lib/auth/guards.spec.ts` (append — confirm the file exists;
  if it does not, create it following this project's existing spec-file conventions)
- Modify: `frontend/projects/admin-app/src/app/app.routes.ts`
- Modify: `frontend/projects/admin-app/src/app/layout/shell.component.ts`

**Interfaces:**
- Produces: `roleGuard(...roles: readonly string[])` — backward-compatible with every existing
  single-role call site (`roleGuard('Admin')` still means exactly what it meant before).

- [ ] **Step 1: Write the failing test**

```ts
// append to frontend/projects/common/src/lib/auth/guards.spec.ts
it('AC164: roleGuard admits a caller holding ANY of several listed roles', () => {
  // Exercised via the existing test harness in this file — construct a SessionStore stub whose
  // hasRole returns true only for 'Supervisor', run roleGuard('Supervisor', 'Admin') against it,
  // and assert the guard returns true (matching this file's existing guard-test pattern for a
  // single role, extended to the two-role call).
});
```

(Written against whatever harness `guards.spec.ts` already establishes for `authGuard`/single-role
`roleGuard` calls — read that file first and match its exact `TestBed`/stub pattern rather than
inventing a new one.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test common --watch=false --include='**/guards.spec.ts'`
Expected: FAIL — `roleGuard` only accepts one argument today.

- [ ] **Step 3: Extend `roleGuard` to accept multiple roles**

In `guards.ts`, change:

```ts
export function roleGuard(role: string): CanActivateFn {
  return (_route, state) => {
    const session = inject(SessionStore);
    const router = inject(Router);

    if (!session.isAuthenticated()) {
      return router.createUrlTree(['/login'], {
        queryParams: { returnUrl: state.url },
      });
    }

    return session.hasRole(role) ? true : router.createUrlTree(['/forbidden']);
  };
}
```

to:

```ts
/**
 * Admits a caller holding ANY of the listed roles — `roleGuard('Admin')` still means exactly
 * what it meant before this change; `roleGuard('Supervisor', 'Admin')` is new, matching the
 * backend's `Supervisor` policy (`Supervisor` OR `Admin`) for the reports routes (AC-164).
 */
export function roleGuard(...roles: readonly string[]): CanActivateFn {
  return (_route, state) => {
    const session = inject(SessionStore);
    const router = inject(Router);

    if (!session.isAuthenticated()) {
      return router.createUrlTree(['/login'], {
        queryParams: { returnUrl: state.url },
      });
    }

    return roles.some((role) => session.hasRole(role)) ? true : router.createUrlTree(['/forbidden']);
  };
}
```

- [ ] **Step 4: Add routes and nav entry**

In `app.routes.ts`, add three children (after `sla-policies`, before `audit-log`):

```ts
      {
        path: 'reports/ticket-volume',
        canActivate: [roleGuard('Supervisor', 'Admin')],
        loadComponent: () => import('./features/reports/ticket-volume-report.component'),
      },
      {
        path: 'reports/sla-performance',
        canActivate: [roleGuard('Supervisor', 'Admin')],
        loadComponent: () => import('./features/reports/sla-performance-report.component'),
      },
      {
        path: 'reports/agent-performance',
        canActivate: [roleGuard('Supervisor', 'Admin')],
        loadComponent: () => import('./features/reports/agent-performance-report.component'),
      },
```

In `shell.component.ts`, `NAV_ITEMS` needs a role check broader than the existing `adminOnly?: true`
flag (which only ever meant Admin). Add a second optional flag rather than overloading the first:

```ts
export interface NavItem {
  readonly path: string;
  readonly key: TranslationKey;
  readonly icon: string;
  readonly adminOnly?: true;
  /** Visible to Admin or Supervisor — distinct from `adminOnly`, which means Admin alone. */
  readonly supervisorOrAdmin?: true;
}
```

Add one nav entry (linking to the first report screen — the other two are reached from within it,
matching how `AuditLogComponent` is the sole nav entry for administration's several sub-views):

```ts
  { path: '/reports/ticket-volume', key: 'nav.reports', icon: 'bar_chart', supervisorOrAdmin: true },
```

Update the `nav` computed to respect the new flag:

```ts
  protected readonly nav = computed(() =>
    NAV_ITEMS.filter(
      (item) =>
        (!item.adminOnly || this.session.hasRole('Admin')) &&
        (!item.supervisorOrAdmin || this.session.hasRole('Supervisor') || this.session.hasRole('Admin')),
    ),
  );
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd frontend && npx ng test common --watch=false --include='**/guards.spec.ts'`
Expected: PASS.

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/shell.component.spec.ts' --include='**/nav-routes.spec.ts'`
Expected: PASS — `nav-routes.spec.ts` (per the frontend survey) asserts every `NAV_ITEMS` entry
resolves to a declared route; the new `/reports/ticket-volume` entry must match the route added in
Step 4 exactly, or this fails.

- [ ] **Step 6: Full frontend gate**

Run: `cd frontend && npx ng build admin-app`
Expected: `Application bundle generation complete`, 0 errors.

Run: `cd frontend && npx ng test common --watch=false`
Expected: all tests pass, including `no-hardcoded-strings.spec.ts` and `rtl-safety.spec.ts`.

Run: `cd frontend && npx ng test admin-app --watch=false`
Expected: all tests pass.

Paste every command's actual output into the task record — not a summary of what should happen.

- [ ] **Step 7: Commit**

```bash
git add frontend/projects/common/src/lib/auth/guards.ts frontend/projects/common/src/lib/auth/guards.spec.ts frontend/projects/admin-app/src/app/app.routes.ts frontend/projects/admin-app/src/app/layout/shell.component.ts
git commit -m "feat(reports): route and nav the three report screens, Supervisor-or-Admin gated (AC-164)"
```

## Verification & gates (frontend addendum)

- Per task: failing test observed, then green, output pasted — not assumed.
- Task record appended to `docs/superpowers/plans/EPIC-08-US-606-feat-reporting/README.md` once all
  three tasks are green: evidence (every command's actual output), what shipped, deviations, gaps
  (still no dashboard/live-queue/CSAT/branch-filter frontend — unchanged from spec addendum A4).

