# US-605 CSAT Report — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** A customer satisfaction rating data source (nothing like it exists today) and a
CSAT-by-language / CSAT-by-priority report over it, matching the shape of the three real reports
already shipped.

**Architecture:** One new immutable entity (`CsatResponse`), one submission command reachable from
the customer portal, one query/handler under `Features/Reports/` mirroring
`GetTicketVolumeReportQueryHandler`'s exact shape, one controller action on the existing
`ReportsController`.

**Tech Stack:** .NET 10, EF Core, MediatR — no new packages.

**Spec:** `docs/superpowers/specs/EPIC-08-EPIC-08-US-605-csat-report.md`, shared design
`docs/superpowers/specs/EPIC-08-US-606-reporting.md` (assumption A2 — this story was cut
entirely because the rating mechanism didn't exist; this plan is what closes that gap).

**Not implemented — plan only**, per this codebase's Sprint-13/9/11 precedent of stopping at
planning for not-yet-built epics.

## Global Constraints

- `ReportsController`'s existing `[Authorize(Policy = "Supervisor")]` covers this new action too —
  no new authorization code needed.
- No department/branch scope parameter (spec A1 of the shared reporting design — that data doesn't
  exist yet either). If `US-608` ever lands, this report's query gets the same scope predicate
  every other report query gets, not a bespoke one.
- Every new failure code registered in `SystemCode.cs`/`SystemCodeMap.cs`/
  `ResponseExtensions.MapFailureStatusCode` — the standing lesson from `FEAT-16`'s own task record.

---

### Task 1: `CsatResponse` entity, submission command, migration

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Reports/CsatResponse.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/CsatResponseConfiguration.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/SubmitCsatResponse/`
  (Command + Handler + Validator)
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (or the
  portal-facing equivalent, if submission is customer-initiated — confirm against `US-408`/`US-409`'s
  survey-response story, which this shares a shape with, before picking the host)
- Test: `backend/tests/CustomerSupport.Tests/Integration/CsatResponseEndpointTests.cs`

**Interfaces:**
- Produces: `CsatResponse(Guid Id, Guid TicketId, int Rating, string Language, string Channel,
  DateTime SubmittedAtUtc, DateTime CreatedAt)` — `Rating` 1–5, validated in `Create`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "605.1")]
public async Task AC605_1_SubmitCsat_ValidRating_IsRecorded()
{
    var ticketId = await CreateResolvedTicketAsync();

    var response = await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/csat",
        new { rating = 5, language = "en", channel = "Email" });

    response.StatusCode.Should().Be(HttpStatusCode.Created);
}

[Fact]
[Trait("AC", "605.1")]
public async Task AC605_1_SubmitCsat_SecondResponseForSameTicket_Returns409()
{
    var ticketId = await CreateResolvedTicketAsync();
    await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/csat", new { rating = 5, language = "en", channel = "Email" });

    var response = await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/csat",
        new { rating = 2, language = "en", channel = "Email" });

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
[Trait("AC", "605.1")]
public async Task AC605_1_SubmitCsat_RatingOutOfRange_Returns400()
{
    var ticketId = await CreateResolvedTicketAsync();

    var response = await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/csat", new { rating = 6, language = "en", channel = "Email" });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC605_1"`
Expected: FAIL — route doesn't exist.

- [ ] **Step 3: Entity**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Reports/CsatResponse.cs
using CustomerSupport.Domain.Entities;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Domain.Entities.Reports;

/// <summary>One customer's satisfaction rating for a resolved ticket — AC-605.1. Append-only:
/// a rating, once given, is never edited, matching this codebase's IAppendOnlyEntity guard.</summary>
public class CsatResponse : BaseEntity, IAppendOnlyEntity
{
    public Guid TicketId { get; private set; }
    public int Rating { get; private set; }
    public string Language { get; private set; } = "en";
    public string Channel { get; private set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; private set; }

    public static CsatResponse Create(Guid ticketId, int rating, string language, string channel)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }

        return new CsatResponse
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Rating = rating,
            Language = language,
            Channel = channel,
            SubmittedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
```

EF config: unique index on `TicketId` (one response per ticket — the `409` in `AC605_1`'s second
test), plus indexes on `SubmittedAtUtc` for the report query's date-range filter.

- [ ] **Step 4: Command + handler, `IDbExceptionTranslator` pairing**

```csharp
// backend/src/CustomerSupport.Application/Features/Tickets/Commands/SubmitCsatResponse/SubmitCsatResponseCommand.cs
public record SubmitCsatResponseCommand(Guid TicketId, int Rating, string Language, string Channel)
    : ICommand<Response<Guid>>;
```

Handler loads the ticket (404 if missing, matches every other ticket-scoped command in this
codebase), constructs `CsatResponse.Create(...)`, `AddAsync`, catches `IsUniqueViolation` via
`IDbExceptionTranslator` → `CSAT_ALREADY_SUBMITTED` (409) — the same pairing convention every
other unique index in this codebase follows since `FEAT-16`'s own README documented the lesson.
`ArgumentOutOfRangeException` from `Rating` validation → 400 (`ResponseValidationBehavior`'s
existing catch-all, or a dedicated FluentValidation rule on the command's own validator, matching
this codebase's stated preference for validators over domain exceptions where the check is a
simple range).

- [ ] **Step 5: Register error codes, run test to verify it passes**

`ApplicationErrors.cs`: `CSAT_ALREADY_SUBMITTED`. `SystemCode.cs`/`SystemCodeMap.cs`: new `ERRnn`,
mapped. `ResponseExtensions.MapFailureStatusCode`: add to the `409` arm.

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC605_1"`
Expected: PASS, 3/3.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Reports/CsatResponse.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/CsatResponseConfiguration.cs \
        backend/src/CustomerSupport.Application/Features/Tickets/Commands/SubmitCsatResponse/ \
        backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/CsatResponseEndpointTests.cs
git commit -m "feat(reports): CSAT response entity and submission (AC-605.1)"
```

---

### Task 2: CSAT report query and endpoint

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Reports/Dtos/CsatReportDto.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Reports/Queries/GetCsatReport/`
  (Query + Handler + Validator, mirroring `GetTicketVolumeReport*` exactly)
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs`
- Modify: `backend/tests/CustomerSupport.Tests/Integration/CsatResponseEndpointTests.cs` (append)

**Interfaces:**
- Consumes: `IRepository<CsatResponse>`, `IRepository<Ticket>` (for `totalTickets`).
- Produces: `CsatReportDto(IReadOnlyList<CsatBucket> ByLanguage, IReadOnlyList<CsatBucket>
  ByChannel)`, `CsatBucket(string Key, double AverageRating, int TotalResponses, int TotalTickets)`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "605.2")]
public async Task AC605_2_CsatReport_GroupsByLanguageAndChannel()
{
    var t1 = await CreateResolvedTicketAsync();
    await _client.PostAsJsonAsync($"/api/Tickets/{t1}/csat", new { rating = 4, language = "en", channel = "Email" });
    var t2 = await CreateResolvedTicketAsync();
    // t2 left unrated deliberately — must not be treated as a zero rating.

    var report = await _client.GetFromJsonAsync<Response<CsatReportRow>>(
        $"/api/reports/csat?{Range()}");

    var enBucket = report!.Data!.ByLanguage.Single(b => b.Key == "en");
    enBucket.TotalResponses.Should().Be(1);
    enBucket.AverageRating.Should().Be(4.0);
}

public sealed record CsatBucketRow(string Key, double AverageRating, int TotalResponses, int TotalTickets);
public sealed record CsatReportRow(IReadOnlyList<CsatBucketRow> ByLanguage, IReadOnlyList<CsatBucketRow> ByChannel);
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC605_2"`
Expected: FAIL — route doesn't exist.

- [ ] **Step 3: DTO + handler**

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Dtos/CsatReportDto.cs
public record CsatBucket(string Key, double AverageRating, int TotalResponses, int TotalTickets);
public record CsatReportDto(IReadOnlyList<CsatBucket> ByLanguage, IReadOnlyList<CsatBucket> ByChannel);
```

```csharp
// backend/src/CustomerSupport.Application/Features/Reports/Queries/GetCsatReport/GetCsatReportQueryHandler.cs
public class GetCsatReportQueryHandler(
    IRepository<CsatResponse> responses,
    IRepository<Ticket> tickets,
    IMessageFactory messages)
    : IQueryHandler<GetCsatReportQuery, Response<CsatReportDto>>
{
    public async Task<Response<CsatReportDto>> Handle(GetCsatReportQuery request, CancellationToken ct)
    {
        var rows = await responses.ListProjectedAsync(
            r => r.SubmittedAtUtc >= request.From && r.SubmittedAtUtc <= request.To,
            r => new { r.Language, r.Channel, r.Rating },
            ct);

        var ticketCountInRange = await tickets.CountAsync(
            t => t.CreatedAt >= request.From && t.CreatedAt <= request.To, ct);

        var byLanguage = rows.GroupBy(r => r.Language)
            .Select(g => new CsatBucket(g.Key, g.Average(x => x.Rating), g.Count(), ticketCountInRange))
            .ToList();

        var byChannel = rows.GroupBy(r => r.Channel)
            .Select(g => new CsatBucket(g.Key, g.Average(x => x.Rating), g.Count(), ticketCountInRange))
            .ToList();

        return messages.Success(new CsatReportDto(byLanguage, byChannel), ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
```

`totalTickets` here is deliberately the whole range's ticket count, not a per-bucket count — the
story's own contract JSON in this plan's earlier draft used the same "how many tickets could have
rated" denominator per group; a truer per-language/per-channel ticket count would need
`Ticket.Language`/`Ticket.Channel` fields that don't exist, so this is the honest approximation,
noted here rather than silently assumed exact.

- [ ] **Step 4: Controller action, run test to verify it passes**

`ReportsController`: `GET /api/reports/csat`, same `from`/`to` validator shape as the three
existing report queries (reuse the shared `DateRangeQueryValidator` base if one exists after
`US-610`'s shared-contract task, otherwise a standalone validator matching
`GetTicketVolumeReportQueryValidator`'s `To >= From` rule exactly).

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC605"`
Expected: PASS, 4/4 (both tasks' tests).

- [ ] **Step 5: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Reports/Dtos/CsatReportDto.cs \
        backend/src/CustomerSupport.Application/Features/Reports/Queries/GetCsatReport/ \
        backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/CsatResponseEndpointTests.cs
git commit -m "feat(reports): CSAT report by language and channel (AC-605.2)"
```

## Definition of done

`AC-605.1`, `AC-605.2` each covered by a test naming it · `dotnet build` clean · full-suite
regression run. **No frontend this pass** — matches every other not-yet-built epic's precedent of
stopping at a backend-only plan.
