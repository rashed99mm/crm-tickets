# Task 2 — Impact/Urgency → derived priority (US-923, AC-923.1…AC-923.6)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/ValueObjects/TicketImpact.cs`
- Create: `backend/src/CustomerSupport.Domain/ValueObjects/TicketUrgency.cs`
- Create: `backend/src/CustomerSupport.Domain/ValueObjects/PriorityMatrix.cs`
- Modify: `backend/src/CustomerSupport.Domain/ValueObjects/TicketChangeType.cs` (add `Reprioritized`)
- Modify: `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` (`Create` at lines 96–161, new `Reclassify`, `UpdateDetails` loses `priority` at lines 405–429)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommand.cs`
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandValidator.cs` (priority rules at lines 27-29 replaced)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs:48-55`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ReclassifyTicket/ReclassifyTicketCommand.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ReclassifyTicket/ReclassifyTicketCommandValidator.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ReclassifyTicket/ReclassifyTicketCommandHandler.cs`
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs` (both DTOs)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQueryHandler.cs:43,70-88`
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs` (append DTO args)
- Modify: `backend/src/CustomerSupport.Application/Features/Ai/Chat/AiChatFeatures.cs:290-295` (fixes the pre-existing invalid `Priority: "Medium"` — that path fails validation today)
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (`Create` at 114-131, new `Reclassify` endpoint)
- Modify: `backend/src/CustomerSupport.ExternalApi/Controllers/PortalController.cs:92-98` (+ the `PortalCreateTicketRequest` record — find it with `grep -rn "record PortalCreateTicketRequest" backend/src`, drop its `Priority` field)
- Modify: `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs` (Validation consts + `Ticket.RECLASSIFIED`)
- Modify: `backend/src/CustomerSupport.Application/Messages/SystemCode.cs`, `SystemCodeMap.cs`
- Modify: `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml`
- Modify: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`
- Test: `backend/tests/CustomerSupport.Tests/Unit/Domain/PriorityMatrixTests.cs` (new)
- Test: `backend/tests/CustomerSupport.Tests/Unit/Domain/TicketReclassifyTests.cs` (new)
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketClassificationEndpointTests.cs` (new)
- Modify: every test fixture sending `priority` — enumerate with `grep -rn "priority" backend/tests --include=*.cs -il` and `grep -rn "Ticket.Create(" backend/src backend/tests --include=*.cs`

**Interfaces:**
- Consumes: Task 1's `ResolutionDetails` (fixtures resolve tickets); `ChangeTicketStatusCommandValidator.BeBase64`
  (internal static, same assembly — reuse for the new validator); handler auth/save idiom from
  `ChangeTicketStatusCommandHandler.cs:21-68`.
- Produces (later tasks and the frontend plan rely on):
  - `sealed class TicketImpact` / `TicketUrgency` — `Value`, statics `Low|Medium|High`, `Create`, `TryCreate`, `All`
  - `static class PriorityMatrix { static TicketPriority Derive(TicketImpact, TicketUrgency) }`
  - `Ticket.Create(string reference, string subject, string description, Guid customerId, Guid categoryId, string impact, string urgency, Guid actorId)` — **priority parameter is gone**
  - `Ticket.Impact : string?`, `Ticket.Urgency : string?`, `Ticket.Reclassify(string impact, string urgency, Guid actorId)`
  - `TicketChangeType.Reprioritized` (`"Reprioritized"`)
  - `CreateTicketCommand(string Subject, string Description, Guid CustomerId, Guid CategoryId, string? Impact = null, string? Urgency = null, string? Source = null)`
  - `POST /api/tickets/{id}/classification` with `ReclassifyTicketRequest(string Impact, string Urgency, string RowVersion)`
  - Detail DTO gains `string? Impact, string? Urgency` appended after `ReopenCount`; list DTO after `EscalationAssigneeId`.

## Steps

- [ ] **Step 1: Write the failing matrix tests — all nine cells**

Create `backend/tests/CustomerSupport.Tests/Unit/Domain/PriorityMatrixTests.cs`:

```csharp
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>US-923 / AC-923.1 — the 3×3 matrix, exhaustively. The spec's table is the oracle.</summary>
public class PriorityMatrixTests
{
    [Theory]
    [Trait("AC", "923.1")]
    [InlineData("Low", "Low", "Low")]
    [InlineData("Low", "Medium", "Low")]
    [InlineData("Low", "High", "Normal")]
    [InlineData("Medium", "Low", "Low")]
    [InlineData("Medium", "Medium", "Normal")]
    [InlineData("Medium", "High", "High")]
    [InlineData("High", "Low", "Normal")]
    [InlineData("High", "Medium", "High")]
    [InlineData("High", "High", "Urgent")]
    public void Derives_The_Spec_Matrix(string impact, string urgency, string expectedPriority)
    {
        var derived = PriorityMatrix.Derive(TicketImpact.Create(impact), TicketUrgency.Create(urgency));

        derived.Value.Should().Be(expectedPriority);
    }

    [Theory]
    [Trait("AC", "923.1")]
    [InlineData("")]
    [InlineData("Critical")]
    public void Unknown_Impact_Is_Refused(string impact)
    {
        var act = () => TicketImpact.Create(impact);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [Trait("AC", "923.1")]
    [InlineData("")]
    [InlineData("Immediate")]
    public void Unknown_Urgency_Is_Refused(string urgency)
    {
        var act = () => TicketUrgency.Create(urgency);
        act.Should().Throw<ArgumentException>();
    }
}
```

Create `backend/tests/CustomerSupport.Tests/Unit/Domain/TicketReclassifyTests.cs`:

```csharp
using CustomerSupport.Domain.Entities.Tickets;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>US-923 — creation derives, reclassify re-derives and records (AC-923.1/2).</summary>
public class TicketReclassifyTests
{
    private static readonly Guid Customer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Category = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Supervisor = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static Ticket NewTicket(string impact = "Medium", string urgency = "Medium") =>
        Ticket.Create("TKT-001000", "Cannot sign in", "The portal rejects my password.",
            Customer, Category, impact, urgency, Supervisor);

    [Fact]
    [Trait("AC", "923.1")]
    public void Creation_Derives_Priority_From_The_Matrix()
    {
        var ticket = NewTicket("High", "High");

        ticket.Impact.Should().Be("High");
        ticket.Urgency.Should().Be("High");
        ticket.Priority.Should().Be("Urgent");
    }

    [Fact]
    [Trait("AC", "923.2")]
    public void Reclassify_Rederives_And_Records_History_When_Priority_Changes()
    {
        var ticket = NewTicket("Medium", "Medium"); // Normal

        ticket.Reclassify("High", "High", Supervisor); // Urgent

        ticket.Priority.Should().Be("Urgent");
        ticket.History.Should().Contain(h =>
            h.ChangeType == "Reprioritized" && h.FromValue == "Normal" && h.ToValue == "Urgent");
    }

    [Fact]
    [Trait("AC", "923.2")]
    public void Reclassify_Without_A_Priority_Change_Writes_No_History_Row()
    {
        var ticket = NewTicket("Medium", "Medium"); // Normal

        ticket.Reclassify("Low", "High", Supervisor); // still Normal

        ticket.Impact.Should().Be("Low");
        ticket.Urgency.Should().Be("High");
        ticket.Priority.Should().Be("Normal");
        ticket.History.Should().NotContain(h => h.ChangeType == "Reprioritized");
    }

    [Fact]
    [Trait("AC", "923.2")]
    public void Reclassify_Requires_An_Actor()
    {
        var ticket = NewTicket();

        var act = () => ticket.Reclassify("High", "High", Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~PriorityMatrixTests|FullyQualifiedName~TicketReclassifyTests"
```

Expected: compile errors (`TicketImpact`, `PriorityMatrix`, 8-arg `Ticket.Create` missing).

- [ ] **Step 3: Implement the domain**

`TicketImpact.cs` — clone the `TicketPriority.cs` shape exactly, values `Low|Medium|High`:

```csharp
namespace CustomerSupport.Domain.ValueObjects;

/// <summary>How widely the incident hurts (US-923). One axis of the priority matrix.</summary>
public sealed class TicketImpact : ValueObject
{
    public string Value { get; }

    public static readonly TicketImpact Low = new("Low");
    public static readonly TicketImpact Medium = new("Medium");
    public static readonly TicketImpact High = new("High");

    public static IReadOnlyList<TicketImpact> All { get; } = [Low, Medium, High];

    private TicketImpact(string value)
    {
        Value = value;
    }

    public static TicketImpact Create(string? impact)
    {
        if (string.IsNullOrWhiteSpace(impact))
        {
            throw new ArgumentException("Impact is required", nameof(impact));
        }

        return impact.Trim() switch
        {
            "Low" => Low,
            "Medium" => Medium,
            "High" => High,
            _ => throw new ArgumentException(
                $"Invalid ticket impact: {impact}. Must be Low, Medium, or High.", nameof(impact))
        };
    }

    public static bool TryCreate(string? impact, out TicketImpact? result, out string? error)
    {
        try
        {
            result = Create(impact);
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

    public static implicit operator string(TicketImpact impact) => impact.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

`TicketUrgency.cs` — identical shape, class name `TicketUrgency`, messages say "urgency"
(`"Urgency is required"`, `"Invalid ticket urgency: {urgency}. Must be Low, Medium, or High."`).
Write it out in full — do not alias or subclass.

`PriorityMatrix.cs`:

```csharp
namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// The one place priority comes from (US-923, spec decision 2026-08-31: matrix-only, no override).
/// Pure — a business rule, not a service.
/// </summary>
public static class PriorityMatrix
{
    public static TicketPriority Derive(TicketImpact impact, TicketUrgency urgency) =>
        (impact.Value, urgency.Value) switch
        {
            ("Low", "Low") => TicketPriority.Low,
            ("Low", "Medium") => TicketPriority.Low,
            ("Low", "High") => TicketPriority.Normal,
            ("Medium", "Low") => TicketPriority.Low,
            ("Medium", "Medium") => TicketPriority.Normal,
            ("Medium", "High") => TicketPriority.High,
            ("High", "Low") => TicketPriority.Normal,
            ("High", "Medium") => TicketPriority.High,
            ("High", "High") => TicketPriority.Urgent,
            _ => throw new ArgumentOutOfRangeException(nameof(impact),
                $"No matrix cell for impact '{impact.Value}' × urgency '{urgency.Value}'."),
        };
}
```

`TicketChangeType.cs` — add the static field after `Escalated` (line 16), extend `All` (line 18)
and the `Create` switch (line 33) and its error message:

```csharp
    public static readonly TicketChangeType Reprioritized = new("Reprioritized");
```

`Ticket.cs`:

1. New properties after `ReopenCount` (Task 1's block):

```csharp
    /// <summary>
    /// US-923. The matrix inputs. Null on tickets created before FEAT-32 (spec A1) — their stored
    /// Priority stands until the first Reclassify.
    /// </summary>
    public string? Impact { get; private set; }
    public string? Urgency { get; private set; }
```

2. `Create` — replace the `string priority` parameter with `string impact, string urgency`
   (between `categoryId` and `actorId`); replace the `priorityVo` line (140) and the assignment
   block:

```csharp
        var impactVo = TicketImpact.Create(impact);
        var urgencyVo = TicketUrgency.Create(urgency);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Reference = reference.Trim(),
            Subject = subject.Trim(),
            Description = description,
            CustomerId = customerId,
            CategoryId = categoryId,
            Impact = impactVo.Value,
            Urgency = urgencyVo.Value,
            Priority = PriorityMatrix.Derive(impactVo, urgencyVo).Value,
            Status = TicketStatus.New.Value,
            AssigneeId = null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId
        };
```

3. New method after `UpdateDetails`:

```csharp
    /// <summary>
    /// US-923 / AC-923.2. Sets the matrix inputs and re-derives priority — the only mutation path
    /// priority has (spec decision: matrix-only). A changed derivation is recorded; an unchanged
    /// one is not history, because nothing the queue sorts on moved.
    /// </summary>
    public void Reclassify(string impact, string urgency, Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(actorId));
        }

        var impactVo = TicketImpact.Create(impact);
        var urgencyVo = TicketUrgency.Create(urgency);
        var derived = PriorityMatrix.Derive(impactVo, urgencyVo).Value;

        Impact = impactVo.Value;
        Urgency = urgencyVo.Value;

        if (derived != Priority)
        {
            var previous = Priority;
            Priority = derived;
            Append(actorId, TicketChangeType.Reprioritized, previous, derived);
        }

        MarkUpdated();
        UpdatedBy = actorId;
    }
```

4. `UpdateDetails` (lines 405–429): delete the `string? priority` parameter and its
   `if (!string.IsNullOrWhiteSpace(priority)) { ... }` block. The method keeps subject/description.

- [ ] **Step 4: Fix every `Ticket.Create` caller and run the domain tests**

```bash
grep -rn "Ticket.Create(" backend/src backend/tests --include=*.cs
```

Expected callers: `CreateTicketCommandHandler.cs:48` (Step 6 rewrites it), `TicketTests.cs:27`,
`TicketResolutionTests` (Task 1), and any seeder. Update test fixtures to
`..., Customer, Category, "Medium", "Medium", Supervisor)` — `Medium/Medium` derives `Normal`, the
old fixtures' value, so no assertion moves. Then:

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~PriorityMatrixTests|FullyQualifiedName~TicketReclassifyTests|FullyQualifiedName~TicketTests|FullyQualifiedName~TicketResolutionTests"
```

Expected: PASS.

- [ ] **Step 5: Commit the domain slice**

```bash
git add backend/src/CustomerSupport.Domain backend/tests/CustomerSupport.Tests/Unit/Domain
git commit -m "feat: derive ticket priority from an impact/urgency matrix (AC-923.1..2)"
```

- [ ] **Step 6: Message codes — all four registrations**

`ApplicationErrors.cs` `Validation` class (after Task 1's resolution consts):

```csharp
        // US-923 — impact/urgency classification (AC-923.1).
        public const string TICKET_IMPACT_REQUIRED = "TICKET_IMPACT_REQUIRED";
        public const string TICKET_IMPACT_INVALID = "TICKET_IMPACT_INVALID";
        public const string TICKET_URGENCY_REQUIRED = "TICKET_URGENCY_REQUIRED";
        public const string TICKET_URGENCY_INVALID = "TICKET_URGENCY_INVALID";
```

`ApplicationErrors.cs` `Ticket` class (after `MODIFIED_BY_ANOTHER_USER`, ~line 205):

```csharp
        /// <summary>US-923 / AC-923.2 — reclassification applied, priority re-derived.</summary>
        public const string RECLASSIFIED = "TICKET_RECLASSIFIED";
```

`SystemCode.cs` (after `VAL070`):

```csharp
        public const string VAL071 = "VAL071"; // Ticket impact required (AC-923.1)
        public const string VAL072 = "VAL072"; // Ticket impact invalid (AC-923.1)
        public const string VAL073 = "VAL073"; // Ticket urgency required (AC-923.1)
        public const string VAL074 = "VAL074"; // Ticket urgency invalid (AC-923.1)

        public const string CON074 = "CON074"; // Ticket reclassified (AC-923.2)
```

`SystemCodeMap.cs` (same block as Task 1's entries):

```csharp
        ["TICKET_IMPACT_REQUIRED"] = SystemCode.VAL071,
        ["TICKET_IMPACT_INVALID"] = SystemCode.VAL072,
        ["TICKET_URGENCY_REQUIRED"] = SystemCode.VAL073,
        ["TICKET_URGENCY_INVALID"] = SystemCode.VAL074,
        ["TICKET_RECLASSIFIED"] = SystemCode.CON074,
```

`Resources.yaml` (after Task 1's blocks):

```yaml
TICKET_IMPACT_REQUIRED:
  ar: "درجة التأثير مطلوبة"
  en: "Impact is required"

TICKET_IMPACT_INVALID:
  ar: "درجة التأثير يجب أن تكون Low أو Medium أو High"
  en: "Impact must be Low, Medium, or High"

TICKET_URGENCY_REQUIRED:
  ar: "درجة الإلحاح مطلوبة"
  en: "Urgency is required"

TICKET_URGENCY_INVALID:
  ar: "درجة الإلحاح يجب أن تكون Low أو Medium أو High"
  en: "Urgency must be Low, Medium, or High"

TICKET_RECLASSIFIED:
  ar: "تم إعادة تصنيف التذكرة"
  en: "Ticket reclassified"
```

- [ ] **Step 7: Write the failing integration tests**

Create `backend/tests/CustomerSupport.Tests/Integration/TicketClassificationEndpointTests.cs`
(same fixture skeleton as Task 1's endpoint tests — factory, supervisor client, customer +
category in `InitializeAsync`):

```csharp
using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>US-923 — matrix-only priority on the wire (AC-923.1/2/3/5/6).</summary>
public class TicketClassificationEndpointTests : IAsyncLifetime
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
            email = $"classification-{Guid.NewGuid():N}@example.com",
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    public Task DisposeAsync()
    {
        _supervisor.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private object CreatePayload(string impact = "High", string urgency = "High") => new
    {
        subject = "Cannot sign in",
        description = "The portal rejects my password.",
        customerId = _customerId,
        categoryId = _categoryId,
        impact,
        urgency,
    };

    [Fact]
    [Trait("AC", "923.1")]
    public async Task Create_Without_Impact_And_Urgency_Is_A_400_Naming_Both_Fields()
    {
        var response = await _supervisor.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot sign in",
            description = "The portal rejects my password.",
            customerId = _customerId,
            categoryId = _categoryId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "Impact");
        body.Errors.Should().Contain(e => e.Field == "Urgency");
    }

    [Fact]
    [Trait("AC", "923.1")]
    [Trait("AC", "923.6")]
    public async Task Create_Derives_Priority_And_The_Detail_Carries_The_Inputs()
    {
        var created = await _supervisor.PostAsJsonAsync("/api/Tickets", CreatePayload("High", "High"));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        var detail = await _supervisor.GetFromJsonAsync<Response<ClassifiedDetail>>($"/api/Tickets/{id}");
        detail!.Data!.Priority.Should().Be("Urgent");
        detail.Data.Impact.Should().Be("High");
        detail.Data.Urgency.Should().Be("High");
    }

    [Fact]
    [Trait("AC", "923.3")]
    public async Task A_Priority_Field_In_The_Body_Has_No_Effect()
    {
        var created = await _supervisor.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot sign in",
            description = "The portal rejects my password.",
            customerId = _customerId,
            categoryId = _categoryId,
            impact = "Low",
            urgency = "Low",
            priority = "Urgent", // must be inert — the contract no longer has it
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        var detail = await _supervisor.GetFromJsonAsync<Response<ClassifiedDetail>>($"/api/Tickets/{id}");
        detail!.Data!.Priority.Should().Be("Low");
    }

    [Fact]
    [Trait("AC", "923.2")]
    public async Task Reclassify_Rederives_And_Writes_A_Reprioritized_History_Row()
    {
        var created = await _supervisor.PostAsJsonAsync("/api/Tickets", CreatePayload("Medium", "Medium"));
        var id = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        var before = await _supervisor.GetFromJsonAsync<Response<ClassifiedDetail>>($"/api/Tickets/{id}");

        var response = await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/classification",
            new { impact = "High", urgency = "High", rowVersion = before!.Data!.RowVersion });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await _supervisor.GetFromJsonAsync<Response<ClassifiedDetail>>($"/api/Tickets/{id}");
        after!.Data!.Priority.Should().Be("Urgent");
        after.Data.History.Should().Contain(h =>
            h.ChangeType == "Reprioritized" && h.FromValue == "Normal" && h.ToValue == "Urgent");
    }

    [Fact]
    [Trait("AC", "923.1")]
    public async Task Reclassify_With_An_Unknown_Impact_Is_A_400()
    {
        var created = await _supervisor.PostAsJsonAsync("/api/Tickets", CreatePayload());
        var id = (await created.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        var before = await _supervisor.GetFromJsonAsync<Response<ClassifiedDetail>>($"/api/Tickets/{id}");

        var response = await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/classification",
            new { impact = "Critical", urgency = "High", rowVersion = before!.Data!.RowVersion });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "Impact");
    }

    private sealed record ClassifiedDetail(
        Guid Id, string Priority, string? Impact, string? Urgency, string RowVersion,
        IReadOnlyList<HistoryRow> History);

    private sealed record HistoryRow(string ChangeType, string? FromValue, string? ToValue);
}
```

AC-923.5 (portal defaults) is asserted where the portal already has coverage — find the portal
ticket-creation test with `grep -rn "portal" backend/tests --include=*.cs -il`, drop `priority`
from its payload, and assert the created ticket's priority is `Normal`. If no portal creation test
exists, add one test method to this class using the ExternalApi factory only if one exists in the
suite; otherwise record the gap in the task's completion note rather than inventing a host fixture.

- [ ] **Step 8: Run to verify failure, then implement application + API**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketClassificationEndpointTests"
```

Expected: FAIL (400s not produced; `/classification` 404s).

`CreateTicketCommand.cs`:

```csharp
public record CreateTicketCommand(
    string Subject,
    string Description,
    Guid CustomerId,
    Guid CategoryId,

    /// <summary>The matrix inputs (US-923). Required from the staff surface; customer-origin
    /// callers (portal, channels, AI handover) omit them and the handler defaults both to
    /// Medium — deriving Normal, the old default priority (spec A2).</summary>
    string? Impact = null,
    string? Urgency = null,

    /// <summary>The channel the ticket originated on. <c>Portal</c> for the customer-facing host.
    /// Null from the staff surface — an agent-authored ticket carries no source (PJ-5/US-404).</summary>
    string? Source = null) : ICommand<Response<Guid>>;

/// <summary>The create payload — AC-29, AC-30, AC-923.1. Priority is not accepted (spec A10).</summary>
public record CreateTicketRequest(
    string Subject,
    string Description,
    Guid CustomerId,
    Guid CategoryId,
    string Impact,
    string Urgency);
```

`CreateTicketCommandValidator.cs` — replace the `Priority` rules (lines 27-29) with:

```csharp
        // US-923 / AC-923.1. Required from the staff surface (Source == null); customer-origin
        // callers omit both and the handler defaults them (spec A2). Whenever present, they must
        // be real matrix values regardless of origin.
        When(x => x.Source is null, () =>
        {
            RuleFor(x => x.Impact)
                .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_IMPACT_REQUIRED);
            RuleFor(x => x.Urgency)
                .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_URGENCY_REQUIRED);
        });

        When(x => !string.IsNullOrWhiteSpace(x.Impact), () =>
            RuleFor(x => x.Impact)
                .Must(v => TicketImpact.TryCreate(v, out _, out _))
                .WithErrorCode(ApplicationErrors.Validation.TICKET_IMPACT_INVALID));

        When(x => !string.IsNullOrWhiteSpace(x.Urgency), () =>
            RuleFor(x => x.Urgency)
                .Must(v => TicketUrgency.TryCreate(v, out _, out _))
                .WithErrorCode(ApplicationErrors.Validation.TICKET_URGENCY_INVALID));
```

Delete the now-unused `BeAKnownPriority` helper (lines 42-43).

`CreateTicketCommandHandler.cs` — replace the `Ticket.Create` call (lines 48-55):

```csharp
        var ticket = Ticket.Create(
            reference,
            request.Subject,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            request.Impact ?? TicketImpact.Medium.Value,
            request.Urgency ?? TicketUrgency.Medium.Value,
            userContext.UserId);
```

(add `using CustomerSupport.Domain.ValueObjects;`.)

New `ReclassifyTicketCommand.cs`:

```csharp
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.ReclassifyTicket;

/// <summary>
/// Sets a ticket's impact/urgency and re-derives its priority (US-923, AC-923.2). RowVersion is
/// echoed for the same lost-update reason as <c>ChangeTicketStatusCommand</c>.
/// </summary>
public record ReclassifyTicketCommand(Guid TicketId, string Impact, string Urgency, string RowVersion)
    : ICommand<Response<Guid>>;

/// <summary>The classification payload. <c>RowVersion</c> is the value read from the detail endpoint.</summary>
public record ReclassifyTicketRequest(string Impact, string Urgency, string RowVersion);
```

New `ReclassifyTicketCommandValidator.cs`:

```csharp
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Tickets.Commands.ChangeTicketStatus;
using CustomerSupport.Domain.ValueObjects;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.ReclassifyTicket;

/// <summary>AC-923.1 — both matrix inputs, always, plus the concurrency token's shape.</summary>
public class ReclassifyTicketCommandValidator : AbstractValidator<ReclassifyTicketCommand>
{
    public ReclassifyTicketCommandValidator()
    {
        RuleFor(x => x.Impact)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_IMPACT_REQUIRED)
            .Must(v => TicketImpact.TryCreate(v, out _, out _))
            .WithErrorCode(ApplicationErrors.Validation.TICKET_IMPACT_INVALID);

        RuleFor(x => x.Urgency)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_URGENCY_REQUIRED)
            .Must(v => TicketUrgency.TryCreate(v, out _, out _))
            .WithErrorCode(ApplicationErrors.Validation.TICKET_URGENCY_INVALID);

        RuleFor(x => x.RowVersion)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.ROW_VERSION_REQUIRED)
            .Must(ChangeTicketStatusCommandValidator.BeBase64)
            .WithErrorCode(ApplicationErrors.Validation.ROW_VERSION_REQUIRED);
    }
}
```

New `ReclassifyTicketCommandHandler.cs` — the auth + concurrency idiom is copied from
`ChangeTicketStatusCommandHandler.cs:21-68`, deliberately:

```csharp
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Commands.ReclassifyTicket;

/// <summary>
/// US-923 / AC-923.2. Same per-record rule as a status change: an agent may reclassify only a
/// ticket assigned to them; a supervisor/admin may reclassify any — decidable only with the ticket
/// loaded.
/// </summary>
public class ReclassifyTicketCommandHandler(
    IRepository<Ticket> tickets,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IUserContext userContext,
    IMessageFactory messages)
    : ICommandHandler<ReclassifyTicketCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(ReclassifyTicketCommand request, CancellationToken ct)
    {
        var ticket = await tickets.GetTrackedAsync(request.TicketId, ct);

        if (ticket is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var isSupervisor = userContext.HasAnyRole(ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin);
        if (!isSupervisor && !ticket.IsAssignedTo(userContext.UserId))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.NOT_ASSIGNED_TO_YOU, MessageType.Forbidden);
        }

        ticket.Reclassify(request.Impact, request.Urgency, userContext.UserId);

        tickets.SetOriginalValue(ticket, nameof(Ticket.RowVersion), Convert.FromBase64String(request.RowVersion));

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsConcurrencyViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.MODIFIED_BY_ANOTHER_USER, MessageType.Conflict);
        }

        return messages.Success(ticket.Id, ApplicationErrors.Ticket.RECLASSIFIED);
    }
}
```

`TicketDtos.cs` — append to `TicketDetailDto` (after Task 1's `ReopenCount`):

```csharp
    // US-923 / AC-923.6. Null on tickets created before FEAT-32 (spec A1).
    string? Impact,
    string? Urgency);
```

and to `TicketListItemDto` (after `EscalationAssigneeId`):

```csharp
    // US-923 / AC-923.6.
    string? Impact,
    string? Urgency);
```

`GetTicketsQueryHandler.cs` — add `t.Impact, t.Urgency` to the anonymous projection (line 43) and
`t.Impact, t.Urgency` as the final arguments of the `TicketListItemDto` construction (line 88).

`GetTicketByIdQueryHandler.cs` — append `ticket.Impact, ticket.Urgency` after Task 1's
`ticket.ReopenCount`.

`AiChatFeatures.cs:290-295` — the handover currently sends `Priority: "Medium"`, which
`TicketPriority.Create` refuses (`Low/Normal/High/Urgent`), so this path 400s today. Replace:

```csharp
        var created = await mediator.Send(new CreateTicketCommand(
            Subject: "Handed over from AI assistant",
            Description: transcript,
            CustomerId: customerId!.Value,
            CategoryId: categoryId.Value,
            Impact: "Medium",
            Urgency: "Medium"), ct);
```

`TicketsController.cs` — `Create` (lines 114-131) passes the new fields:

```csharp
        var result = await mediator.Send(
            new CreateTicketCommand(
                request.Subject,
                request.Description,
                request.CustomerId,
                request.CategoryId,
                request.Impact,
                request.Urgency),
            ct);
```

and a new endpoint after `ChangeStatus` (after line 178):

```csharp
    /// <summary>Sets a ticket's impact and urgency; priority is re-derived by the matrix (US-923).</summary>
    /// <remarks>
    /// Priority has no direct setter anywhere on the surface (spec decision: matrix-only). A
    /// changed derivation writes a <c>Reprioritized</c> history row; an unchanged one does not.
    /// </remarks>
    [HttpPost("{id:guid}/classification")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reclassify(Guid id, [FromBody] ReclassifyTicketRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ReclassifyTicketCommand(id, request.Impact, request.Urgency, request.RowVersion),
            ct);

        return this.ToActionResult(result);
    }
```

(add `using CustomerSupport.Application.Features.Tickets.Commands.ReclassifyTicket;`.)

`PortalController.cs:92-98` — the customer no longer picks a priority (spec A2):

```csharp
        var command = new CreateTicketCommand(
            request.Subject,
            request.Description,
            customerId,
            request.CategoryId,
            Source: PortalSource);
```

and remove `Priority` from the `PortalCreateTicketRequest` record (locate with
`grep -rn "record PortalCreateTicketRequest" backend/src`).

`TicketConfiguration.cs` — after Task 1's resolution block:

```csharp
        // US-923. Matrix inputs; null means "created before FEAT-32, never reclassified" (spec A1).
        builder.Property(x => x.Impact).HasMaxLength(8);
        builder.Property(x => x.Urgency).HasMaxLength(8);
```

- [ ] **Step 9: Migration**

```bash
dotnet ef migrations add AddImpactUrgencyClassification --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi
```

Inspect: exactly two nullable `AddColumn` on `Tickets`. No data backfill (spec A1).

- [ ] **Step 10: Update every fixture that still sends `priority`, run everything**

```bash
grep -rn "priority" backend/tests --include=*.cs -l
```

For each hit that posts to `/api/Tickets` (e.g. `TicketLifecycleEndpointTests.cs:75-85`,
`TicketEndpointTests`, `TicketResolutionEndpointTests` from Task 1, SLA/report fixtures): replace
`priority = "Normal"` with `impact = "Medium", urgency = "Medium"`, and where a fixture needs a
specific priority for an SLA policy or report assertion, pick the matrix cell that derives it
(`High/High`→Urgent, `High/Medium`→High, `Medium/Medium`→Normal, `Low/Low`→Low). The AC-923.4
regression is the existing SLA integration suite passing unchanged in its assertions.

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketClassificationEndpointTests"
cd backend && dotnet test CustomerSupport.slnx
```

Expected: new tests PASS; full suite green — paste the summary line.

- [ ] **Step 11: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat: matrix-only priority on the wire, classification endpoint, portal/AI defaults (AC-923.1..6)"
```
