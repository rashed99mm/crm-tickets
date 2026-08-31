# Task 1 — Resolution discipline (US-922, AC-922.1…AC-922.6)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/ValueObjects/TicketResolutionCode.cs`
- Create: `backend/src/CustomerSupport.Domain/ValueObjects/ResolutionDetails.cs`
- Modify: `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` (fields after `EscalationAssigneeId` ~line 82; `ChangeStatus` at lines 170–218)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommand.cs`
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandValidator.cs`
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandHandler.cs:49`
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs:33-65` (detail DTO)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs:67-100`
- Modify: `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs` (`Validation` class, after `TICKET_SOURCE_INVALID` ~line 305)
- Modify: `backend/src/CustomerSupport.Application/Messages/SystemCode.cs` (after `VAL066`, line 214)
- Modify: `backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs` (validation block, after `["TICKET_STATUS_REQUIRED"]` ~line 180)
- Modify: `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml` (after `TICKET_PRIORITY_INVALID` ~line 540)
- Modify: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfiguration.cs` (after the `EscalationState` property, line 31)
- Test: `backend/tests/CustomerSupport.Tests/Unit/Domain/TicketResolutionTests.cs` (new)
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketResolutionEndpointTests.cs` (new)

**Interfaces:**
- Consumes: `Ticket.ChangeStatus(string, Guid)` (`Ticket.cs:170`), `TicketStatus.IsReopenTo`,
  `messages.Validation<T>` / `MessageType.Conflict` envelope machinery, `CrmApiFactory` +
  `CreateAuthenticatedClientAsync` test fixtures (see `TicketLifecycleEndpointTests.cs:34-103`).
- Produces (later tasks rely on these exact names):
  - `Ticket.ResolutionCode : string?`, `Ticket.ResolutionNotes : string?`, `Ticket.ReopenCount : int`
  - `sealed class TicketResolutionCode` with `Value`, statics `Fixed|Workaround|Duplicate|CannotReproduce|NoResponse`, `Create`, `TryCreate`, `All`
  - `sealed record ResolutionDetails(string Code, string Notes)` (namespace `CustomerSupport.Domain.ValueObjects`)
  - `Ticket.ChangeStatus(string targetStatus, Guid actorId, ResolutionDetails? resolution = null)`
  - `ChangeTicketStatusCommand(Guid TicketId, string Status, string RowVersion, string? ResolutionCode = null, string? ResolutionNotes = null)` and the request record with the same two optional fields
  - Task 4 adds the Duplicate-link 409 inside this same handler.

## Steps

- [ ] **Step 1: Branch**

```bash
git checkout -b feat/feat-32-ticket-domain-enrichment
```

- [ ] **Step 2: Write the failing domain tests**

Create `backend/tests/CustomerSupport.Tests/Unit/Domain/TicketResolutionTests.cs`. Fixture helpers
copy the `TicketTests` pattern (`TicketTests.cs:16-63`):

```csharp
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>US-922 — how a ticket was resolved is recorded, and reopening is counted (AC-922.2/4/5).</summary>
public class TicketResolutionTests
{
    private static readonly Guid Customer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Category = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Supervisor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Agent = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly ResolutionDetails Resolution = new("Fixed", "Reset the password and confirmed sign-in.");

    private static Ticket TicketInProgress()
    {
        var ticket = Ticket.Create("TKT-001000", "Cannot sign in", "The portal rejects my password.",
            Customer, Category, "Normal", Supervisor);
        ticket.AssignTo(Agent, Supervisor);
        ticket.ChangeStatus("Open", Agent);
        ticket.ChangeStatus("Assigned", Agent);
        ticket.ChangeStatus("In Progress", Agent);
        return ticket;
    }

    [Fact]
    [Trait("AC", "922.5")]
    public void Resolving_Without_Details_Is_Refused()
    {
        var ticket = TicketInProgress();

        var act = () => ticket.ChangeStatus("Resolved", Agent);

        act.Should().Throw<InvalidOperationException>().WithMessage("*resolution*");
        ticket.Status.Should().Be("In Progress");
        ticket.ResolutionCode.Should().BeNull();
    }

    [Fact]
    [Trait("AC", "922.2")]
    public void Resolving_With_Details_Stamps_Code_Notes_And_ResolvedAt()
    {
        var ticket = TicketInProgress();

        ticket.ChangeStatus("Resolved", Agent, Resolution);

        ticket.Status.Should().Be("Resolved");
        ticket.ResolutionCode.Should().Be("Fixed");
        ticket.ResolutionNotes.Should().Be("Reset the password and confirmed sign-in.");
        ticket.ResolvedAt.Should().NotBeNull();
        ticket.History.Should().Contain(h => h.ChangeType == "StatusChanged" && h.ToValue == "Resolved");
    }

    [Fact]
    [Trait("AC", "922.3")]
    public void An_Unknown_Resolution_Code_Is_Refused()
    {
        var ticket = TicketInProgress();

        var act = () => ticket.ChangeStatus("Resolved", Agent, new ResolutionDetails("Solved", "notes"));

        act.Should().Throw<ArgumentException>();
        ticket.Status.Should().Be("In Progress");
    }

    [Theory]
    [Trait("AC", "922.3")]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_Resolution_Notes_Are_Refused(string notes)
    {
        var ticket = TicketInProgress();

        var act = () => ticket.ChangeStatus("Resolved", Agent, new ResolutionDetails("Fixed", notes));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "922.3")]
    public void Resolution_Notes_Over_2000_Chars_Are_Refused()
    {
        var ticket = TicketInProgress();

        var act = () => ticket.ChangeStatus("Resolved", Agent, new ResolutionDetails("Fixed", new string('x', 2001)));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "922.4")]
    public void Reopening_Clears_Resolution_And_Increments_ReopenCount()
    {
        var ticket = TicketInProgress();
        ticket.ChangeStatus("Resolved", Agent, Resolution);

        ticket.ChangeStatus("In Progress", Agent); // reopen (IsReopenTo targets In Progress)

        ticket.ReopenCount.Should().Be(1);
        ticket.ResolutionCode.Should().BeNull();
        ticket.ResolutionNotes.Should().BeNull();
        ticket.ResolvedAt.Should().BeNull();
        ticket.History.Should().Contain(h => h.ChangeType == "Reopened");
    }

    [Fact]
    [Trait("AC", "922.4")]
    public void Every_Reopen_Counts()
    {
        var ticket = TicketInProgress();
        ticket.ChangeStatus("Resolved", Agent, Resolution);
        ticket.ChangeStatus("In Progress", Agent);
        ticket.ChangeStatus("Resolved", Agent, Resolution);
        ticket.ChangeStatus("In Progress", Agent);

        ticket.ReopenCount.Should().Be(2);
    }

    [Fact]
    [Trait("AC", "922.2")]
    public void Closing_A_Resolved_Ticket_Keeps_The_Resolution()
    {
        var ticket = TicketInProgress();
        ticket.ChangeStatus("Resolved", Agent, Resolution);

        ticket.ChangeStatus("Closed", Agent);

        ticket.ResolutionCode.Should().Be("Fixed");
        ticket.ResolutionNotes.Should().NotBeNull();
    }
}
```

> Note: `Ticket.Create(..., "Normal", ...)` still takes a priority string in this slice — Task 2
> changes that signature and updates this fixture to `"Medium", "Medium"` in the same commit.

- [ ] **Step 3: Run to verify failure**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketResolutionTests"
```

Expected: compile errors — `ResolutionDetails` and `Ticket.ResolutionCode` do not exist yet. A
compile failure is this step's red.

- [ ] **Step 4: Implement the domain**

Create `backend/src/CustomerSupport.Domain/ValueObjects/TicketResolutionCode.cs` — same shape as
`TicketPriority.cs` (name is `TicketResolutionCode`, not `ResolutionCode`, because
`Ticket.ResolutionCode` the *property* would otherwise shadow the type inside `Ticket`):

```csharp
namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// How a ticket was resolved (US-922, AC-922.2). Five values fixed by the FEAT-32 spec; persisted
/// as a string for the same reason as <see cref="TicketStatus"/>.
/// </summary>
public sealed class TicketResolutionCode : ValueObject
{
    public string Value { get; }

    public static readonly TicketResolutionCode Fixed = new("Fixed");
    public static readonly TicketResolutionCode Workaround = new("Workaround");
    public static readonly TicketResolutionCode Duplicate = new("Duplicate");
    public static readonly TicketResolutionCode CannotReproduce = new("CannotReproduce");
    public static readonly TicketResolutionCode NoResponse = new("NoResponse");

    public static IReadOnlyList<TicketResolutionCode> All { get; } =
        [Fixed, Workaround, Duplicate, CannotReproduce, NoResponse];

    private TicketResolutionCode(string value)
    {
        Value = value;
    }

    public static TicketResolutionCode Create(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A resolution code is required", nameof(code));
        }

        return code.Trim() switch
        {
            "Fixed" => Fixed,
            "Workaround" => Workaround,
            "Duplicate" => Duplicate,
            "CannotReproduce" => CannotReproduce,
            "NoResponse" => NoResponse,
            _ => throw new ArgumentException(
                $"Invalid resolution code: {code}. Must be Fixed, Workaround, Duplicate, CannotReproduce, or NoResponse.",
                nameof(code))
        };
    }

    public static bool TryCreate(string? code, out TicketResolutionCode? result, out string? error)
    {
        try
        {
            result = Create(code);
            error = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            result = null;
            error = ex.Message;
            return false;
        }
    }

    public static implicit operator string(TicketResolutionCode code) => code.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

Create `backend/src/CustomerSupport.Domain/ValueObjects/ResolutionDetails.cs`:

```csharp
namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// What the resolver must state when a ticket enters <c>Resolved</c> (US-922). Validated inside
/// <c>Ticket.ChangeStatus</c> — this record is the carrier, not the rule.
/// </summary>
public sealed record ResolutionDetails(string Code, string Notes);
```

Modify `Ticket.cs`. Fields, inserted directly after `EscalationAssigneeId` (line 82):

```csharp
    /// <summary>
    /// US-922 / AC-922.2. How the ticket was resolved — required on the transition into
    /// <c>Resolved</c>, cleared on reopen. Null on a ticket that has never been resolved.
    /// </summary>
    public string? ResolutionCode { get; private set; }
    public string? ResolutionNotes { get; private set; }

    /// <summary>US-922 / AC-922.4. How many times a resolved/closed ticket was sent back.</summary>
    public int ReopenCount { get; private set; }
```

`ChangeStatus` — the signature gains the optional parameter, and the resolved/reopen block
(currently lines 194–208) is replaced:

```csharp
    public void ChangeStatus(string targetStatus, Guid actorId, ResolutionDetails? resolution = null)
    {
        // ... existing actor / transition-table / assignee guards unchanged (lines 172-192) ...

        var isReopen = current.IsReopenTo(target);
        var changeType = isReopen ? TicketChangeType.Reopened : TicketChangeType.StatusChanged;

        // US-906 / AC-510: entering Resolved/Closed stamps the respective timestamp; reopening clears
        // both so the next resolve starts clean. US-922: the resolution record follows the same
        // lifecycle — required to enter Resolved (AC-922.5), cleared and counted on reopen (AC-922.4).
        if (isReopen)
        {
            ResolvedAt = null;
            ClosedAt = null;
            ResolutionCode = null;
            ResolutionNotes = null;
            ReopenCount++;
        }
        else
        {
            if (target.Value == "Resolved")
            {
                if (resolution is null)
                {
                    throw new InvalidOperationException(
                        $"Ticket '{Reference}' cannot be resolved without a resolution code and notes.");
                }

                var code = TicketResolutionCode.Create(resolution.Code);

                if (string.IsNullOrWhiteSpace(resolution.Notes))
                {
                    throw new ArgumentException("Resolution notes are required", nameof(resolution));
                }

                if (resolution.Notes.Length > 2000)
                {
                    throw new ArgumentException("Resolution notes must not exceed 2000 characters", nameof(resolution));
                }

                ResolutionCode = code.Value;
                ResolutionNotes = resolution.Notes.Trim();
                ResolvedAt = DateTime.UtcNow;
            }

            if (target.Value == "Closed") ClosedAt = DateTime.UtcNow;
        }

        // ... rest of the method unchanged (Status assignment, MarkUpdated, SLA pause, Append, event) ...
    }
```

- [ ] **Step 5: Run the domain tests**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketResolutionTests"
```

Expected: PASS (8 tests). Also run the neighbouring suites that call `ChangeStatus` — the new
parameter is optional so they must still compile and pass:

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketTests|FullyQualifiedName~TicketStatusTests"
```

**Known consequence:** any existing unit/integration test that drives a ticket into `Resolved`
now fails with the new guard. Find them before running the full suite:
`grep -rn "\"Resolved\"" backend/tests --include=*.cs`. Update each call site to pass
`new ResolutionDetails("Fixed", "resolved in test")` (unit) — integration fixtures are updated in
Step 8's request payloads. This is AC-922.5 doing its job, not collateral damage.

- [ ] **Step 6: Commit the domain slice**

```bash
git add backend/src/CustomerSupport.Domain backend/tests/CustomerSupport.Tests/Unit/Domain/TicketResolutionTests.cs
git commit -m "feat: require resolution code and notes to resolve a ticket (AC-922.2..5)"
```

(Include any unit-test call sites updated in Step 5.)

- [ ] **Step 7: Message codes — all four registrations**

`ApplicationErrors.cs`, inside `public static class Validation`, after `TICKET_SOURCE_INVALID`
(line 305):

```csharp
        // US-922 — resolution discipline (AC-922.1/3).
        public const string RESOLUTION_CODE_REQUIRED = "RESOLUTION_CODE_REQUIRED";
        public const string RESOLUTION_CODE_INVALID = "RESOLUTION_CODE_INVALID";
        public const string RESOLUTION_NOTES_REQUIRED = "RESOLUTION_NOTES_REQUIRED";
        public const string RESOLUTION_NOTES_MAX_LENGTH = "RESOLUTION_NOTES_MAX_LENGTH";
```

`SystemCode.cs`, after `VAL066` (line 214):

```csharp
        public const string VAL067 = "VAL067"; // Resolution code required (AC-922.1)
        public const string VAL068 = "VAL068"; // Resolution code invalid (AC-922.3)
        public const string VAL069 = "VAL069"; // Resolution notes required (AC-922.1)
        public const string VAL070 = "VAL070"; // Resolution notes too long (AC-922.3)
```

`SystemCodeMap.cs`, in the ticket-validation block after `["TICKET_STATUS_REQUIRED"]` (line 180):

```csharp
        ["RESOLUTION_CODE_REQUIRED"] = SystemCode.VAL067,
        ["RESOLUTION_CODE_INVALID"] = SystemCode.VAL068,
        ["RESOLUTION_NOTES_REQUIRED"] = SystemCode.VAL069,
        ["RESOLUTION_NOTES_MAX_LENGTH"] = SystemCode.VAL070,
```

`Resources.yaml`, after the `TICKET_PRIORITY_INVALID` block (line 540):

```yaml
RESOLUTION_CODE_REQUIRED:
  ar: "رمز الحل مطلوب عند حل التذكرة"
  en: "A resolution code is required to resolve a ticket"

RESOLUTION_CODE_INVALID:
  ar: "رمز الحل يجب أن يكون Fixed أو Workaround أو Duplicate أو CannotReproduce أو NoResponse"
  en: "Resolution code must be Fixed, Workaround, Duplicate, CannotReproduce, or NoResponse"

RESOLUTION_NOTES_REQUIRED:
  ar: "ملاحظات الحل مطلوبة عند حل التذكرة"
  en: "Resolution notes are required to resolve a ticket"

RESOLUTION_NOTES_MAX_LENGTH:
  ar: "ملاحظات الحل يجب ألا تتجاوز 2000 حرف"
  en: "Resolution notes must not exceed 2000 characters"
```

- [ ] **Step 8: Write the failing integration tests**

Create `backend/tests/CustomerSupport.Tests/Integration/TicketResolutionEndpointTests.cs`,
following the fixture idioms of `TicketLifecycleEndpointTests.cs:23-103` (factory, authenticated
clients, `CreateTicketAsync`, `DetailAsync` reading `RowVersion`):

```csharp
using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>US-922 — the wire half of resolution discipline (AC-922.1/2/3/4/6).</summary>
public class TicketResolutionEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _supervisor = null!;
    private Guid _categoryId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_supervisor, _) = await _factory.CreateAuthenticatedClientAsync("Supervisor");
        _categoryId = await _factory.EnsureCategoryAsync("Technical");

        var customer = await _supervisor.PostAsJsonAsync("/api/Customers", new
        {
            name = "Layla Haddad",
            email = $"resolution-{Guid.NewGuid():N}@example.com",
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    public Task DisposeAsync()
    {
        _supervisor.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> TicketAtOpenAsync()
    {
        var created = await _supervisor.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot sign in",
            description = "The portal rejects my password.",
            customerId = _customerId,
            categoryId = _categoryId,
            priority = "Normal",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        (await ChangeStatusAsync(id, "Open")).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private async Task<string> RowVersionAsync(Guid id)
    {
        var detail = await _supervisor.GetFromJsonAsync<Response<TicketResolutionDetail>>($"/api/Tickets/{id}");
        return detail!.Data!.RowVersion;
    }

    private async Task<HttpResponseMessage> ChangeStatusAsync(
        Guid id, string status, string? resolutionCode = null, string? resolutionNotes = null)
    {
        var rowVersion = await RowVersionAsync(id);
        return await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/status",
            new { status, rowVersion, resolutionCode, resolutionNotes });
    }

    [Fact]
    [Trait("AC", "922.1")]
    public async Task Resolving_Without_Code_Or_Notes_Is_A_400_Naming_Both_Fields()
    {
        var id = await TicketAtOpenAsync();

        var response = await ChangeStatusAsync(id, "Resolved");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "ResolutionCode");
        body.Errors.Should().Contain(e => e.Field == "ResolutionNotes");
    }

    [Fact]
    [Trait("AC", "922.3")]
    public async Task An_Unknown_Resolution_Code_Is_A_400_Naming_The_Field()
    {
        var id = await TicketAtOpenAsync();

        var response = await ChangeStatusAsync(id, "Resolved", "Solved", "notes");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "ResolutionCode");
    }

    [Fact]
    [Trait("AC", "922.2")]
    [Trait("AC", "922.6")]
    public async Task A_Valid_Resolve_Stamps_And_The_Detail_Carries_It()
    {
        var id = await TicketAtOpenAsync();

        var response = await ChangeStatusAsync(id, "Resolved", "Workaround", "Cleared the cache as a stopgap.");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await _supervisor.GetFromJsonAsync<Response<TicketResolutionDetail>>($"/api/Tickets/{id}");
        detail!.Data!.ResolutionCode.Should().Be("Workaround");
        detail.Data.ResolutionNotes.Should().Be("Cleared the cache as a stopgap.");
        detail.Data.ReopenCount.Should().Be(0);
        detail.Data.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("AC", "922.4")]
    public async Task Reopening_Clears_The_Resolution_And_Counts()
    {
        var id = await TicketAtOpenAsync();
        (await ChangeStatusAsync(id, "Resolved", "Fixed", "Reset the password.")).StatusCode.Should().Be(HttpStatusCode.OK);

        // A reopen needs an assignee to enter In Progress (AC-505): supervisor moves it, but the
        // ticket was never assigned — assign first.
        var agents = await _supervisor.GetFromJsonAsync<Response<List<AssignableAgent>>>("/api/Tickets/assignable-agents");
        var rowVersion = await RowVersionAsync(id);
        (await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/assignee",
            new { assigneeId = agents!.Data![0].Id, rowVersion })).StatusCode.Should().Be(HttpStatusCode.OK);

        var reopen = await ChangeStatusAsync(id, "In Progress");

        reopen.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await _supervisor.GetFromJsonAsync<Response<TicketResolutionDetail>>($"/api/Tickets/{id}");
        detail!.Data!.ResolutionCode.Should().BeNull();
        detail.Data.ResolutionNotes.Should().BeNull();
        detail.Data.ReopenCount.Should().Be(1);
    }

    private sealed record TicketResolutionDetail(
        Guid Id, string Status, string RowVersion,
        string? ResolutionCode, string? ResolutionNotes, int ReopenCount, DateTime? ResolvedAt);

    private sealed record AssignableAgent(Guid Id, string Name, string Email);
}
```

> `Response<T>.Errors` — confirm the property name against
> `backend/src/CustomerSupport.Application/Contracts/Response.cs` before running; every existing
> 400-shape test in the suite reads it, copy their accessor if it differs (e.g. a nested envelope).
> The create payload still sends `priority` in this slice; Task 2 rewrites this fixture.

- [ ] **Step 9: Run to verify the integration tests fail**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketResolutionEndpointTests"
```

Expected: FAIL — the request's `resolutionCode` is not yet bound (400s missing, DTO fields absent).

- [ ] **Step 10: Implement the application + API layer**

`ChangeTicketStatusCommand.cs` — extend both records:

```csharp
public record ChangeTicketStatusCommand(
    Guid TicketId, string Status, string RowVersion,
    string? ResolutionCode = null, string? ResolutionNotes = null)
    : ICommand<Response<Guid>>;

/// <summary>The status-change payload. Resolution fields are required when the target is Resolved (AC-922.1).</summary>
public record ChangeTicketStatusRequest(
    string Status, string RowVersion,
    string? ResolutionCode = null, string? ResolutionNotes = null);
```

`ChangeTicketStatusCommandValidator.cs` — append inside the constructor:

```csharp
        // US-922 / AC-922.1: resolution is part of the request's *shape* when the target is
        // Resolved — absent fields are a 400 the form can key to controls, before any state check.
        When(x => IsResolvedTarget(x.Status), () =>
        {
            RuleFor(x => x.ResolutionCode)
                .NotEmpty().WithErrorCode(ApplicationErrors.Validation.RESOLUTION_CODE_REQUIRED)
                .Must(code => TicketResolutionCode.TryCreate(code, out _, out _))
                .WithErrorCode(ApplicationErrors.Validation.RESOLUTION_CODE_INVALID);

            RuleFor(x => x.ResolutionNotes)
                .NotEmpty().WithErrorCode(ApplicationErrors.Validation.RESOLUTION_NOTES_REQUIRED)
                .MaximumLength(2000).WithErrorCode(ApplicationErrors.Validation.RESOLUTION_NOTES_MAX_LENGTH);
        });
```

and the helper below the existing `BeBase64`:

```csharp
    private static bool IsResolvedTarget(string? status) =>
        string.Equals(status?.Trim(), "Resolved", StringComparison.Ordinal);
```

`ChangeTicketStatusCommandHandler.cs` — replace line 49 (`ticket.ChangeStatus(request.Status, userContext.UserId);`):

```csharp
        // AC-922.2. The validator guarantees both fields when the target is Resolved; for any other
        // target stray resolution fields are ignored rather than refused (they change nothing).
        var resolution = request is { ResolutionCode: not null, ResolutionNotes: not null }
            ? new ResolutionDetails(request.ResolutionCode, request.ResolutionNotes)
            : null;

        ticket.ChangeStatus(request.Status, userContext.UserId, resolution);
```

(`CustomerSupport.Domain.ValueObjects` is already imported at line 9.)

`TicketDtos.cs` — append three parameters to the END of `TicketDetailDto` (after
`EscalationAssigneeName`):

```csharp
    // US-922 / AC-922.6. Null / 0 until the ticket has been resolved / reopened.
    string? ResolutionCode,
    string? ResolutionNotes,
    int ReopenCount);
```

`GetTicketByIdQueryHandler.cs` — append the matching arguments to the constructor call (after
`escalationAssigneeName`, line 100):

```csharp
            escalationAssigneeName,
            ticket.ResolutionCode,
            ticket.ResolutionNotes,
            ticket.ReopenCount);
```

`TicketsController.cs` `ChangeStatus` (line 171) — pass the new fields through:

```csharp
        var result = await mediator.Send(
            new ChangeTicketStatusCommand(id, request.Status, request.RowVersion,
                request.ResolutionCode, request.ResolutionNotes),
            ct);
```

`TicketConfiguration.cs` — after the `EscalationState` property (line 31):

```csharp
        // US-922. String-persisted code (same convention as Status/Priority); notes bounded at the
        // validator's limit; ReopenCount defaulted so existing rows backfill to 0.
        builder.Property(x => x.ResolutionCode).HasMaxLength(24);
        builder.Property(x => x.ResolutionNotes).HasMaxLength(2000);
        builder.Property(x => x.ReopenCount).HasDefaultValue(0);
```

- [ ] **Step 11: Migration**

```bash
dotnet ef migrations add AddResolutionDiscipline --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi
```

Inspect the generated file: exactly three `AddColumn` on `Tickets` (`ResolutionCode` nvarchar(24)
null, `ResolutionNotes` nvarchar(2000) null, `ReopenCount` int not null default 0). Anything else
means the snapshot was dirty — stop and investigate before committing.

- [ ] **Step 12: Run the integration tests, then the full suite**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketResolutionEndpointTests"
cd backend && dotnet test CustomerSupport.slnx
```

Expected: new tests PASS; full suite green. Failures naming `Resolved` mean a fixture found in
Step 5's grep was missed — fix it (add `resolutionCode`/`resolutionNotes` to the payload), do not
weaken the guard.

- [ ] **Step 13: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat: resolution fields on the status-change contract and detail DTO (AC-922.1..6)"
```
