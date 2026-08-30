# US-215 — Branch Business-Hours Calendar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** SLA target computation excludes non-working hours and public holidays for a ticket's
branch, replacing the wall-clock-only fallback `FEAT-17`'s first slice shipped with (spec A1:
"without a calendar, targets are computed using wall-clock hours — that fallback *is* what ships
this slice").

**Architecture:** Two new per-branch config entities (`BusinessHoursCalendar`, `PublicHoliday`), one
new application-layer interface `IBusinessHoursCalculator` with an Infrastructure implementation that
`CreateTicketCommandHandler` calls instead of the current bare `AddHours`. Admin CRUD over both
config entities mirrors `SLAPoliciesController`'s exact shape (read `SLAPoliciesController.cs`,
`CreateSLAPolicyCommandHandler.cs`, `SLAPolicyConfiguration.cs` before writing — this plan copies
their constructor injection, controller gating and EF configuration verbatim, substituting the entity
type).

**Spec:** `docs/superpowers/specs/EPIC-05-US-218-sla-tracking.md`, A1.

## Acceptance criteria (continuing from `US-306`'s `AC-224` — next free is `AC-225`)

AC-225. Given a branch has a `BusinessHoursCalendar` row for a day of week, when a ticket is created
for that branch and an SLA policy matches, then the response/resolution due dates are computed by
advancing through configured working windows only — time outside `OpenTime`–`CloseTime` on a
configured day, or on a day with no configured window at all, does not count toward the target hours.

AC-226. Given a date range the calculation crosses includes a `PublicHoliday` row for that branch,
then that entire day is excluded the same way a non-working day is.

AC-227. Given a ticket's branch has no `BusinessHoursCalendar` rows at all, then the calculation
falls back to the existing wall-clock `CreatedAt.AddHours(targetHours)` behavior, unchanged —
matching the story's own Notes and preserving every existing SLA test's assumption.

AC-228. Given an Admin, when they create a `BusinessHoursCalendar` row (`BranchId`, `DayOfWeek`,
`OpenTime`, `CloseTime`) or a `PublicHoliday` row (`BranchId`, `HolidayDate`, `Name`), then it is
stored and retrievable via a paged list.

## Global Constraints

- `IBusinessHoursCalculator` is a new `Application`-layer interface with an `Infrastructure`
  implementation. It reads via `AppDbContext` directly (`db.Set<BusinessHoursCalendar>()`), exactly
  the way the existing `SlaBreachScanner` already reads `db.Set<SLAEvent>()` — `Infrastructure` may
  depend on `AppDbContext`; `Application` only sees the interface. This keeps the dependency rule
  intact (the calculator's lookups would force `Domain` to know about persistence if placed there).
- No new failure codes for the calendar/holiday CRUD themselves. A missing calendar is a *fallback*,
  not an error (`AC-227`), so the create handlers return `ApplicationErrors.General.SUCCESS_CREATED`
  (reusing `CON032`) rather than minting per-entity success codes — matching how `CreateSLAPolicy`
  returns its own `SLA.POLICY_CREATED` only because that code already existed. To stay consistent
  with the rest of the corpus we DO add two new success codes (`CON045`/`CON046`) and map them, see
  Task 1 Step 4.
- `BusinessHoursCalendar.BranchId` is a plain `Guid` column with **no foreign key** to `Branches`,
  mirroring `SLAPolicy.BranchId` (its configuration has no `HasOne`/`WithMany`). This keeps the test
  fixtures free of a branch-FK dependency and matches the "filter column, not a navigation" decision
  already made for `Ticket.BranchId`/`Customer.BranchId`.

---

### Task 1: `BusinessHoursCalendar`/`PublicHoliday` entities + admin CRUD (`AC-228`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Sla/BusinessHoursCalendar.cs`
- Create: `backend/src/CustomerSupport.Domain/Entities/Sla/PublicHoliday.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/BusinessHoursCalendarConfiguration.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/PublicHolidayConfiguration.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Sla/Commands/CreateBusinessHoursCalendar/CreateBusinessHoursCalendarCommand.cs` (+ `Handler`)
- Create: `backend/src/CustomerSupport.Application/Features/Sla/Commands/CreatePublicHoliday/CreatePublicHolidayCommand.cs` (+ `Handler`)
- Create: `backend/src/CustomerSupport.Application/Features/Sla/Queries/GetBusinessHoursCalendars/GetBusinessHoursCalendarsQuery.cs` (+ `Handler`)
- Create: `backend/src/CustomerSupport.Application/Features/Sla/Queries/GetPublicHolidays/GetPublicHolidaysQuery.cs` (+ `Handler`)
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/BusinessHoursController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/BusinessHoursCalendarEndpointTests.cs`

**Interfaces:**
- Produces: `BusinessHoursCalendar(Guid Id, Guid BranchId, DayOfWeek DayOfWeek, TimeOnly OpenTime,
  TimeOnly CloseTime)`, `PublicHoliday(Guid Id, Guid BranchId, DateOnly HolidayDate, string Name)`.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/BusinessHoursCalendarEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

public class BusinessHoursCalendarEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_admin, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Admin);
    }

    public Task DisposeAsync()
    {
        _admin.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateBranchAsync()
    {
        var response = await _admin.PostAsJsonAsync("/api/Branches", new
        {
            name = $"Branch {Guid.NewGuid():N}",
            region = "Riyadh",
            timezone = "Asia/Riyadh",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    [Fact]
    [Trait("AC", "228")]
    public async Task AC228_CreateCalendarRow_Returns201()
    {
        var branchId = await CreateBranchAsync();

        var response = await _admin.PostAsJsonAsync("/api/BusinessHours/calendars", new
        {
            branchId,
            dayOfWeek = "Monday",
            openTime = "09:00",
            closeTime = "17:00",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("AC", "228")]
    public async Task AC228_CreateHoliday_Returns201()
    {
        var branchId = await CreateBranchAsync();

        var response = await _admin.PostAsJsonAsync("/api/BusinessHours/holidays", new
        {
            branchId,
            holidayDate = "2026-12-25",
            name = "Public holiday",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("AC", "228")]
    public async Task AC228_ListCalendars_ReturnsCreatedRow()
    {
        var branchId = await CreateBranchAsync();
        await _admin.PostAsJsonAsync("/api/BusinessHours/calendars", new
        {
            branchId, dayOfWeek = "Tuesday", openTime = "09:00", closeTime = "17:00",
        });

        var list = await _admin.GetFromJsonAsync<Response<PaginatedList<CalendarRow>>>(
            "/api/BusinessHours/calendars?pageSize=50");

        list!.Data!.Items.Should().Contain(r => r.DayOfWeek == "Tuesday");
    }

    private sealed record CalendarRow(Guid Id, Guid BranchId, string DayOfWeek, string OpenTime, string CloseTime);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~BusinessHoursCalendarEndpointTests"`
Expected: FAIL — routes `/api/BusinessHours/calendars` and `/api/BusinessHours/holidays` do not exist.

- [ ] **Step 3: Entities, configs, commands, controller**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Sla/BusinessHoursCalendar.cs
namespace CustomerSupport.Domain.Entities.Sla;

/// <summary>US-215, AC-225 — a working window for one weekday in one branch. Mirror of
/// <see cref="SLAPolicy"/>'s lookup-entity shape (no navigation, soft Active flag pattern).</summary>
public class BusinessHoursCalendar : BaseEntity
{
    public Guid BranchId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly OpenTime { get; private set; }
    public TimeOnly CloseTime { get; private set; }

    public static BusinessHoursCalendar Create(Guid branchId, DayOfWeek dayOfWeek, TimeOnly openTime, TimeOnly closeTime)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId is required", nameof(branchId));
        if (closeTime <= openTime)
            throw new ArgumentException("CloseTime must be after OpenTime", nameof(closeTime));

        return new BusinessHoursCalendar
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            DayOfWeek = dayOfWeek,
            OpenTime = openTime,
            CloseTime = closeTime,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
```

```csharp
// backend/src/CustomerSupport.Domain/Entities/Sla/PublicHoliday.cs
namespace CustomerSupport.Domain.Entities.Sla;

/// <summary>US-215, AC-226 — a whole-day exclusion for one branch. Two holiday rows for the same
/// date are harmless: both simply mark the day excluded.</summary>
public class PublicHoliday : BaseEntity
{
    public Guid BranchId { get; private set; }
    public DateOnly HolidayDate { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public static PublicHoliday Create(Guid branchId, DateOnly holidayDate, string name)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId is required", nameof(branchId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        return new PublicHoliday
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            HolidayDate = holidayDate,
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
    }
}
```

Two EF configurations, both mirroring `SLAPolicyConfiguration` (table name, key, no FK, no unique
index — duplicates are not a conflict case this story defines, matching `SLAPolicy` which also has
no uniqueness beyond `(Priority, IsActive)`):

```csharp
// backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/BusinessHoursCalendarConfiguration.cs
using CustomerSupport.Domain.Entities.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class BusinessHoursCalendarConfiguration : IEntityTypeConfiguration<BusinessHoursCalendar>
{
    public void Configure(EntityTypeBuilder<BusinessHoursCalendar> builder)
    {
        builder.ToTable("BusinessHoursCalendars");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DayOfWeek); // stored as int by EF, fine for an enum
        builder.Property(x => x.OpenTime).HasColumnType("time");
        builder.Property(x => x.CloseTime).HasColumnType("time");
        builder.HasIndex(x => new { x.BranchId, x.DayOfWeek })
            .HasDatabaseName("IX_BusinessHoursCalendars_Branch_Day");
    }
}
```
(`PublicHolidayConfiguration` is the same shape with `ToTable("PublicHolidays")` and
`HasIndex(x => new { x.BranchId, x.HolidayDate })`.)

The four CQRS types copy `CreateSLAPolicyCommand`/`GetSLAPoliciesQuery` structurally. Command
handler — note the **real** `CreateSLAPolicyCommandHandler` constructor is
`(IRepository<X> repo, IUnitOfWork unitOfWork, IMessageFactory messages)` with **no**
`IDbExceptionTranslator` (the plan that claimed otherwise drifted from the code):

```csharp
// backend/src/CustomerSupport.Application/Features/Sla/Commands/CreateBusinessHoursCalendar/CreateBusinessHoursCalendarCommandHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Sla.Commands.CreateBusinessHoursCalendar;

public class CreateBusinessHoursCalendarCommandHandler(
    IRepository<BusinessHoursCalendar> calendars,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<CreateBusinessHoursCalendarCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateBusinessHoursCalendarCommand request, CancellationToken ct)
    {
        var calendar = BusinessHoursCalendar.Create(
            request.BranchId, request.DayOfWeek, request.OpenTime, request.CloseTime);

        await calendars.AddAsync(calendar, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(calendar.Id, ApplicationErrors.BusinessHours.CALENDAR_CREATED);
    }
}
```
`GetBusinessHoursCalendarsQueryHandler` mirrors `GetSLAPoliciesQueryHandler` (unpaged
`ListAsync`, project to a DTO) — `GetPublicHolidaysQueryHandler` identical for holidays.

Controller — `BusinessHoursController` follows `SLAPoliciesController`'s gating exactly
(`[Authorize(Policy = "Authenticated")]` on the class, `[Authorize(Policy = "Admin")]` on the two
`HttpPost` actions, `this.ToActionResult(result, StatusCodes.Status201Created)` on create):

```csharp
// backend/src/CustomerSupport.InternalApi/Controllers/BusinessHoursController.cs (excerpt)
[HttpPost("calendars")]
[Authorize(Policy = "Admin")]
public async Task<IActionResult> CreateCalendar([FromBody] CreateBusinessHoursCalendarRequest request, CancellationToken ct)
{
    var result = await _mediator.Send(new CreateBusinessHoursCalendarCommand(
        request.BranchId, request.DayOfWeek, request.OpenTime, request.CloseTime), ct);
    return this.ToActionResult(result, StatusCodes.Status201Created);
}
```
(`CreateBusinessHoursCalendarRequest` carries `Guid BranchId, string DayOfWeek, string OpenTime,
string CloseTime`; the handler/validator parses `DayOfWeek` via `Enum.Parse<DayOfWeek>` and
`TimeOnly.Parse` — validation failures surface as `400` through FluentValidation, same as
`CreateSLAPolicyRequest`.)

- [ ] **Step 4: Register the new success codes**

`ApplicationErrors.cs`, add a new nested class (mirroring `SLA`):
```csharp
/// <summary>US-215, AC-228.</summary>
public static class BusinessHours
{
    public const string CALENDAR_CREATED = "BUSINESS_HOURS_CALENDAR_CREATED";
    public const string HOLIDAY_CREATED = "BUSINESS_HOURS_HOLIDAY_CREATED";
}
```

`SystemCode.cs`: add after `CON044`
```csharp
public const string CON045 = "CON045"; // Business hours calendar created
public const string CON046 = "CON046"; // Public holiday created
```
`SystemCodeMap.cs`: add
```csharp
["BUSINESS_HOURS_CALENDAR_CREATED"] = SystemCode.CON045,
["BUSINESS_HOURS_HOLIDAY_CREATED"] = SystemCode.CON046,
```
`Resources.yaml`: add ar/en pairs for both keys (required by `EveryErrorCode_HasABilingualMessage`).

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~BusinessHoursCalendarEndpointTests"`
Expected: PASS, 3/3.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Sla/BusinessHoursCalendar.cs \
        backend/src/CustomerSupport.Domain/Entities/Sla/PublicHoliday.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/BusinessHoursCalendarConfiguration.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/PublicHolidayConfiguration.cs \
        backend/src/CustomerSupport.Application/Features/Sla/Commands/CreateBusinessHoursCalendar/ \
        backend/src/CustomerSupport.Application/Features/Sla/Commands/CreatePublicHoliday/ \
        backend/src/CustomerSupport.Application/Features/Sla/Queries/GetBusinessHoursCalendars/ \
        backend/src/CustomerSupport.Application/Features/Sla/Queries/GetPublicHolidays/ \
        backend/src/CustomerSupport.InternalApi/Controllers/BusinessHoursController.cs \
        backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCode.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs \
        backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml \
        backend/tests/CustomerSupport.Tests/Integration/BusinessHoursCalendarEndpointTests.cs
git commit -m "feat(sla): business-hours calendar and public-holiday admin CRUD (US-215, AC-228)"
```

---

### Task 2: `IBusinessHoursCalculator` and wiring into ticket creation (`AC-225`–`AC-227`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Interfaces/IBusinessHoursCalculator.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Sla/BusinessHoursCalculator.cs`
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/BusinessHoursCalculatorTests.cs`

**Interfaces:**
- Produces: `IBusinessHoursCalculator.AddBusinessHours(DateTime start, decimal hours, Guid? branchId, CancellationToken ct) : Task<DateTime>`.

- [ ] **Step 1: Write the failing integration test**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/BusinessHoursCalculatorTests.cs
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

public class BusinessHoursCalculatorTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private Guid _branchId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _branchId = Guid.NewGuid();
        db.Set<BusinessHoursCalendar>().Add(BusinessHoursCalendar.Create(
            _branchId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        db.Set<BusinessHoursCalendar>().Add(BusinessHoursCalendar.Create(
            _branchId, DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        db.Set<BusinessHoursCalendar>().Add(BusinessHoursCalendar.Create(
            _branchId, DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        db.Set<BusinessHoursCalendar>().Add(BusinessHoursCalendar.Create(
            _branchId, DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        db.Set<BusinessHoursCalendar>().Add(BusinessHoursCalendar.Create(
            _branchId, DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private IBusinessHoursCalculator Calculator
        => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IBusinessHoursCalculator>();

    [Fact]
    [Trait("AC", "225")]
    public async Task AC225_SkipsNonWorkingTime()
    {
        // Friday 16:00, add 2 business hours.
        // 1h remains Friday (to 17:00), 1h carries to Monday 09:00-10:00.
        var friday = new DateTime(2026, 10, 2, 16, 0, 0, DateTimeKind.Utc); // a Friday
        var monday = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);

        var result = await Calculator.AddBusinessHours(friday, 2, _branchId, CancellationToken.None);

        result.Should().Be(monday);
    }

    [Fact]
    [Trait("AC", "226")]
    public async Task AC226_SkipsPublicHolidays()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<PublicHoliday>().Add(PublicHoliday.Create(
                _branchId, new DateOnly(2026, 10, 5), "Bridge holiday"));
            await db.SaveChangesAsync();
        }

        // Friday 16:00 + 9h: Fri 1h, Mon is a holiday (0h), Tue 8h -> Tuesday 17:00.
        var friday = new DateTime(2026, 10, 2, 16, 0, 0, DateTimeKind.Utc);
        var tuesday = new DateTime(2026, 10, 6, 17, 0, 0, DateTimeKind.Utc);

        var result = await Calculator.AddBusinessHours(friday, 9, _branchId, CancellationToken.None);

        result.Should().Be(tuesday);
    }

    [Fact]
    [Trait("AC", "227")]
    public async Task AC227_NoCalendarForBranch_FallsBackToWallClock()
    {
        var start = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);

        var result = await Calculator.AddBusinessHours(start, 4, Guid.NewGuid(), CancellationToken.None);

        result.Should().Be(start.AddHours(4));
    }
}
```

(Note: `2026-10-02` is verified a Friday by the test author before committing — if the calendar
math assumes a different weekday, adjust the literals; the assertion is exact, not tolerant, on
purpose.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~BusinessHoursCalculatorTests"`
Expected: FAIL — `IBusinessHoursCalculator` does not exist.

- [ ] **Step 3: Implement**

```csharp
// backend/src/CustomerSupport.Application/Interfaces/IBusinessHoursCalculator.cs
namespace CustomerSupport.Application.Interfaces;

public interface IBusinessHoursCalculator
{
    /// <summary>Advances `start` by `hours` of working time for `branchId`'s calendar. Falls back to
    /// plain wall-clock addition when `branchId` is null or has no configured calendar (US-215,
    /// AC-227) — the exact behavior every existing SLA test already assumes, unchanged.</summary>
    Task<DateTime> AddBusinessHours(DateTime start, decimal hours, Guid? branchId, CancellationToken ct);
}
```

```csharp
// backend/src/CustomerSupport.Infrastructure/Sla/BusinessHoursCalculator.cs
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Sla;

public class BusinessHoursCalculator(AppDbContext db) : IBusinessHoursCalculator
{
    public async Task<DateTime> AddBusinessHours(DateTime start, decimal hours, Guid? branchId, CancellationToken ct)
    {
        if (branchId is not { } branch)
            return start.AddHours((double)hours); // AC-227, no branch at all

        var windows = await db.Set<BusinessHoursCalendar>().IgnoreQueryFilters()
            .Where(c => c.BranchId == branch).ToListAsync(ct);

        if (windows.Count == 0)
            return start.AddHours((double)hours); // AC-227, no configured calendar

        var holidays = (await db.Set<PublicHoliday>().IgnoreQueryFilters()
                .Where(h => h.BranchId == branch).ToListAsync(ct))
            .Select(h => h.HolidayDate).ToHashSet();

        var byDay = windows.ToDictionary(w => w.DayOfWeek);
        var remaining = hours;
        var cursor = start;

        // Bounded loop: 3650 iterations is ten years of calendar days, a generous ceiling for an SLA
        // target that should always resolve in days, not years — and prevents a no-coverage spin.
        for (var i = 0; i < 3650 && remaining > 0; i++)
        {
            var date = DateOnly.FromDateTime(cursor.Date);
            if (holidays.Contains(date) || !byDay.TryGetValue(cursor.DayOfWeek, out var window))
            {
                cursor = cursor.Date.AddDays(1);
                continue;
            }

            var dayStart = cursor.TimeOfDay < window.OpenTime.ToTimeSpan()
                ? window.OpenTime.ToTimeSpan()
                : cursor.TimeOfDay;
            var dayEnd = window.CloseTime.ToTimeSpan();

            if (dayStart >= dayEnd)
            {
                cursor = cursor.Date.AddDays(1);
                continue;
            }

            var availableToday = (decimal)(dayEnd - dayStart).TotalHours;
            var used = Math.Min(remaining, availableToday);

            cursor = cursor.Date + dayStart + TimeSpan.FromHours((double)used);
            remaining -= used;

            if (remaining > 0)
                cursor = cursor.Date.AddDays(1);
        }

        return cursor;
    }
}
```

- [ ] **Step 4: Wire into `CreateTicketCommandHandler` and register the service**

`CreateTicketCommandHandler`'s constructor gains `IBusinessHoursCalculator businessHoursCalculator`
(placed after `IRepository<SLAPolicy> slaPolicies`). Replace the wall-clock lines inside
`ApplySlaTargetsAsync` (`ticket.CreatedAt.AddHours(...)`) with:

```csharp
        ticket.SetSlaTargets(
            await businessHoursCalculator.AddBusinessHours(ticket.CreatedAt, policy.ResponseTargetHours, ticket.BranchId, ct),
            await businessHoursCalculator.AddBusinessHours(ticket.CreatedAt, policy.ResolutionTargetHours, ticket.BranchId, ct));
```

`ApplySlaTargetsAsync` becomes `async` (it already is). In `ServiceCollectionExtensions.RegisterPlatformInfrastructure`,
add alongside the other SLA registrations:

```csharp
        services.AddScoped<IBusinessHoursCalculator, BusinessHoursCalculator>();
```

- [ ] **Step 5: Run tests to verify they pass, and confirm no regression**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~BusinessHoursCalculatorTests|FullyQualifiedName~SlaTrackingEndpointTests|FullyQualifiedName~SlaPauseAndEscalationEndpointTests"`
Expected: PASS — the new calculator tests, and `SlaTrackingEndpointTests`' existing AC-128/AC-129
assertions unchanged. Every existing fixture ticket has `BranchId = null`, so `AC-227`'s fallback
path is exactly what they already exercise — this task must not change their observed due-date
values.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Interfaces/IBusinessHoursCalculator.cs \
        backend/src/CustomerSupport.Infrastructure/Sla/BusinessHoursCalculator.cs \
        backend/src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs \
        backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs \
        backend/tests/CustomerSupport.Tests/Integration/BusinessHoursCalculatorTests.cs
git commit -m "feat(sla): business-hours-aware SLA target computation (US-215, AC-225..227)"
```

## Definition of done

`AC-225` through `AC-228` each covered by a test naming it, including explicit proof that the
existing wall-clock-only tests are unaffected (`AC-227`) · full suite green · task record written to
`docs/superpowers/plans/EPIC-05-US-215-business-hours-calendar/README.md`.

**Load-bearing caveat (engineering, not a disclosure):** `Ticket.BranchId` is never populated by
anything in this codebase today (`FEAT-16`'s own gap, same root cause `US-306` names). The
calculator's fallback rule — "no calendar for this branch → wall-clock, exactly like today" — already
covers a `null` `BranchId` gracefully, so this story is **not** blocked the way `US-306` is: it can
be fully built and tested against a real, explicitly-branched fixture ticket (Task 2's test seeds
`BusinessHoursCalendar` rows under an explicit `_branchId` and asserts against them directly). What
will **not** happen without `US-306`'s own prerequisites resolving is this calculator ever actually
*activating* for a real, organically-created ticket — every ticket created through the normal flow
has `BranchId = null` today and will keep using wall-clock hours regardless of how many calendars an
admin configures. Recorded so a future reader does not mistake "the code works" for "the feature is
live."
