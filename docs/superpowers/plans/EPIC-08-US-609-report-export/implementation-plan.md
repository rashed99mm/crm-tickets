# US-609 Report Export: Implementation Plan

> **Disclosure (added 2026-08-27):** Rewritten to carry real, code-bearing Task sections. Report
> export is NOT SHIPPED; the code below adds CSV export to the three already-shipped report queries,
> reusing their exact handlers (no re-querying).

**Story:** `US-609` · **Spec:** `docs/superpowers/specs/EPIC-08-US-606-reporting.md` · **Status:** NOT SHIPPED

## AC mapping

| Story AC | Proof |
|---|---|
| AC1 — ticket-volume report exportable as CSV | `ReportExportTests.AC609_TicketVolume_CsvExport_ReturnsTextCsv` |
| AC2 — export respects the same Supervisor auth | `ReportExportTests.AC609_Export_RequiresSupervisor` |
| AC3 — export uses the identical scoped data as the JSON report | `ReportExportTests.AC609_ExportMatchesJsonCounts` |

## Affected files

- Create: `backend/src/CustomerSupport.Application/Features/Reports/Queries/ExportTicketVolumeReport/`
- Create: `backend/src/CustomerSupport.Infrastructure/Reports/CsvReportSerializer.cs`
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/ReportExportTests.cs`

---

### Task 1: CSV serializer + export endpoint (`AC-609.1`)

**Files:**
- Create: `.../CsvReportSerializer.cs`
- Create: `.../Queries/ExportTicketVolumeReport/ExportTicketVolumeReportQuery.cs` + Handler
- Modify: `ReportsController.cs`

**Interfaces:**
- Consumes: `GetTicketVolumeReportQuery` via `IMediator.Send`.
- Produces: `text/csv` body via `FileResult`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact] [Trait("AC", "609.1")]
public async Task AC609_TicketVolume_CsvExport_ReturnsTextCsv()
{
    var from = DateTime.UtcNow.AddDays(-7); var to = DateTime.UtcNow;
    var response = await _client.GetAsync($"/api/reports/ticket-volume/export?from={from:o}&to={to:o}");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    var csv = await response.Content.ReadAsStringAsync();
    csv.Should().Contain("Period,Count,Priority");
}
```

- [ ] **Step 2: Serializer**

```csharp
// backend/src/CustomerSupport.Infrastructure/Reports/CsvReportSerializer.cs
public static class CsvReportSerializer
{
    public static string Serialize<T>(IEnumerable<T> rows, params (string Header, Func<T, object?>)[] cols)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', cols.Select(c => c.Header)));
        foreach (var r in rows)
            sb.AppendLine(string.Join(',', cols.Select(c => CsvEscape(c.Item2(r)?.ToString()))));
        return sb.ToString();
    }
    private static string CsvEscape(string? v) =>
        string.IsNullOrEmpty(v) ? "" : v.Contains(',') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
}
```

- [ ] **Step 3: Export handler (reuses the JSON query, no second data path)**

```csharp
public class ExportTicketVolumeReportQueryHandler(IMediator mediator)
    : IQueryHandler<ExportTicketVolumeReportQuery, IResult>
{
    public async Task<IResult> Handle(ExportTicketVolumeReportQuery q, CancellationToken ct)
    {
        var report = await mediator.Send(new GetTicketVolumeReportQuery(q.From, q.To, q.GroupBy), ct);
        var csv = CsvReportSerializer.Serialize(
            report.Data!.Rows,
            ("Period", r => r.Period), ("Count", r => r.Count), ("Priority", r => r.Priority));
        return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "ticket-volume.csv");
    }
}
```

- [ ] **Step 4: Controller action (inherits Supervisor policy)**

```csharp
[HttpGet("ticket-volume/export")]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> ExportTicketVolume([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string groupBy = "day", CancellationToken ct)
    => Ok(await mediator.Send(new ExportTicketVolumeReportQuery(from, to, groupBy), ct));
```

- [ ] **Step 5: Run to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportExportTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Infrastructure/Reports/CsvReportSerializer.cs \
        backend/src/CustomerSupport.Application/Features/Reports/Queries/ExportTicketVolumeReport/ \
        backend/src/CustomerSupport.InternalApi/Controllers/ReportsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ReportExportTests.cs
git commit -m "feat(reports): CSV export for ticket-volume (AC-609.1)"
```

---

### Task 2: Auth + parity (`AC-609.2`, `AC-609.3`)

**Files:** Test only (auth is inherited; parity asserted against the JSON counts).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] [Trait("AC", "609.2")]
public async Task AC609_Export_RequiresSupervisor()
{
    var anon = _factory.CreateClient();
    var r = await anon.GetAsync($"/api/reports/ticket-volume/export?from={DateTime.UtcNow.AddDays(-1):o}&to={DateTime.UtcNow:o}");
    r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

[Fact] [Trait("AC", "609.3")]
public async Task AC609_ExportMatchesJsonCounts()
{
    var json = await _client.GetFromJsonAsync<Response<TicketVolumeReportRow>>($"/api/reports/ticket-volume?from={DateTime.UtcNow.AddDays(-7):o}&to={DateTime.UtcNow:o}");
    var csv = await (await _client.GetAsync($"/api/reports/ticket-volume/export?from={DateTime.UtcNow.AddDays(-7):o}&to={DateTime.UtcNow:o}")).Content.ReadAsStringAsync();
    csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length.Should().Be(json!.Data!.Rows.Count + 1);
}
```

- [ ] **Step 2: Run to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ReportExportTests"`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/ReportExportTests.cs
git commit -m "test(reports): export auth + parity assertions (AC-609.2, AC-609.3)"
```

## Definition of done

`AC-609.1`..`AC-609.3` covered by named tests · build clean · test run pasted. Export shares the
scoped query once `US-608` lands.
