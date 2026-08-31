# Task 4 — Related / duplicate links (US-925, AC-925.1…AC-925.5 API half)

**Cut order note:** cut FIRST if the sprint runs out of time. Cutting it leaves AC-925.3 open —
the `Duplicate` resolution code is then accepted without a link; record that in
`docs/assessment/rubric-traceability.md` under Scope cuts.

**Files:**
- Create: `backend/src/CustomerSupport.Domain/ValueObjects/TicketLinkType.cs`
- Create: `backend/src/CustomerSupport.Domain/Entities/Tickets/TicketLink.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/AddTicketLink/AddTicketLinkCommand.cs` (+ validator + handler)
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/RemoveTicketLink/RemoveTicketLinkCommand.cs` (+ handler)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandHandler.cs` (the AC-925.3 Duplicate-code check)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs` (`TicketLinkDto`, detail `Links`)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs`
- Modify: `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs`, `Messages/SystemCode.cs`, `Messages/SystemCodeMap.cs`
- Modify: `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketLinkConfiguration.cs`
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (two endpoints)
- Test: `backend/tests/CustomerSupport.Tests/Unit/Domain/TicketLinkTests.cs` (new)
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketLinkEndpointTests.cs` (new)

**Interfaces:**
- Consumes: Task 1's resolution contract on `/status` (`resolutionCode`/`resolutionNotes`); Task
  2's create contract; `Ticket.Reference` unique index (`TicketConfiguration.cs:36-38`) for
  reference lookup.
- Produces:
  - `sealed class TicketLinkType` — `Value`, statics `RelatedTo|DuplicateOf`, `Create`, `TryCreate`, `All`
  - `class TicketLink : BaseEntity { Guid SourceTicketId; Guid TargetTicketId; string LinkType; static TicketLink Create(Guid source, Guid target, string linkType, Guid createdBy) }`
  - `POST /api/tickets/{id}/links` (`AddTicketLinkRequest(string LinkType, string TargetReference)`), `DELETE /api/tickets/{id}/links/{linkId}`
  - `TicketLinkDto(Guid Id, string LinkType, string Direction, Guid OtherTicketId, string OtherReference, string OtherSubject)` — `Direction` is `"Outbound"` (this ticket is the source) or `"Inbound"`
  - Detail DTO appends `IReadOnlyList<TicketLinkDto> Links`.

## Steps

- [ ] **Step 1: Write the failing domain tests**

Create `backend/tests/CustomerSupport.Tests/Unit/Domain/TicketLinkTests.cs`:

```csharp
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>US-925 / AC-925.1 — what a link row itself can refuse (the cross-ticket guards are the handler's).</summary>
public class TicketLinkTests
{
    private static readonly Guid Source = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Target = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Theory]
    [Trait("AC", "925.1")]
    [InlineData("RelatedTo")]
    [InlineData("DuplicateOf")]
    public void Creates_A_Link_Of_Each_Type(string linkType)
    {
        var link = TicketLink.Create(Source, Target, linkType, Actor);

        link.SourceTicketId.Should().Be(Source);
        link.TargetTicketId.Should().Be(Target);
        link.LinkType.Should().Be(linkType);
        link.CreatedBy.Should().Be(Actor);
    }

    [Fact]
    [Trait("AC", "925.1")]
    public void Refuses_A_Self_Link()
    {
        var act = () => TicketLink.Create(Source, Source, "RelatedTo", Actor);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "925.1")]
    public void Refuses_An_Unknown_Link_Type()
    {
        var act = () => TicketLink.Create(Source, Target, "BlockedBy", Actor);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [Trait("AC", "925.1")]
    [InlineData("")]
    [InlineData("Related")]
    public void TicketLinkType_Refuses_Unknown_Values(string value)
    {
        var act = () => TicketLinkType.Create(value);
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketLinkTests"
```

Expected: compile errors — the types do not exist.

- [ ] **Step 3: Implement the domain**

`TicketLinkType.cs` — the `TicketPriority` shape, two values:

```csharp
namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// How two tickets relate (US-925). <c>RelatedTo</c> is displayed symmetrically;
/// <c>DuplicateOf</c> is directional — the source is the duplicate, the target the original.
/// </summary>
public sealed class TicketLinkType : ValueObject
{
    public string Value { get; }

    public static readonly TicketLinkType RelatedTo = new("RelatedTo");
    public static readonly TicketLinkType DuplicateOf = new("DuplicateOf");

    public static IReadOnlyList<TicketLinkType> All { get; } = [RelatedTo, DuplicateOf];

    private TicketLinkType(string value)
    {
        Value = value;
    }

    public static TicketLinkType Create(string? linkType)
    {
        if (string.IsNullOrWhiteSpace(linkType))
        {
            throw new ArgumentException("A link type is required", nameof(linkType));
        }

        return linkType.Trim() switch
        {
            "RelatedTo" => RelatedTo,
            "DuplicateOf" => DuplicateOf,
            _ => throw new ArgumentException(
                $"Invalid ticket link type: {linkType}. Must be RelatedTo or DuplicateOf.", nameof(linkType))
        };
    }

    public static bool TryCreate(string? linkType, out TicketLinkType? result, out string? error)
    {
        try
        {
            result = Create(linkType);
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

    public static implicit operator string(TicketLinkType linkType) => linkType.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

`TicketLink.cs`:

```csharp
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Domain.Entities.Tickets;

/// <summary>
/// A pointer between two tickets (US-925) — never a merge. Stored once; the reading side decides
/// how to render direction. Cross-ticket guards (target exists, duplicate row, direct cycle) are
/// the handler's — this entity cannot see other tickets.
/// </summary>
public class TicketLink : BaseEntity
{
    public Guid SourceTicketId { get; private set; }
    public Guid TargetTicketId { get; private set; }
    public string LinkType { get; private set; } = string.Empty;

    public static TicketLink Create(Guid sourceTicketId, Guid targetTicketId, string linkType, Guid createdBy)
    {
        if (sourceTicketId == Guid.Empty)
        {
            throw new ArgumentException("A source ticket is required", nameof(sourceTicketId));
        }

        if (targetTicketId == Guid.Empty)
        {
            throw new ArgumentException("A target ticket is required", nameof(targetTicketId));
        }

        if (sourceTicketId == targetTicketId)
        {
            throw new ArgumentException("A ticket cannot be linked to itself", nameof(targetTicketId));
        }

        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(createdBy));
        }

        return new TicketLink
        {
            Id = Guid.NewGuid(),
            SourceTicketId = sourceTicketId,
            TargetTicketId = targetTicketId,
            LinkType = TicketLinkType.Create(linkType).Value,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }
}
```

- [ ] **Step 4: Run the unit tests, commit the domain slice**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketLinkTests"
git add backend/src/CustomerSupport.Domain backend/tests/CustomerSupport.Tests/Unit/Domain/TicketLinkTests.cs
git commit -m "feat: TicketLink entity and link-type value object (AC-925.1)"
```

- [ ] **Step 5: Message codes — all four registrations**

`ApplicationErrors.cs` `Validation`:

```csharp
        // US-925 — links (AC-925.1).
        public const string TICKET_LINK_TYPE_INVALID = "TICKET_LINK_TYPE_INVALID";
        public const string TICKET_LINK_TARGET_REQUIRED = "TICKET_LINK_TARGET_REQUIRED";
```

`ApplicationErrors.cs` `Ticket`:

```csharp
        // US-925.
        public const string LINK_TARGET_NOT_FOUND = "TICKET_LINK_TARGET_NOT_FOUND";
        public const string LINK_SELF = "TICKET_LINK_SELF";
        public const string LINK_EXISTS = "TICKET_LINK_EXISTS";
        public const string LINK_CYCLE = "TICKET_LINK_CYCLE";
        public const string LINK_NOT_FOUND = "TICKET_LINK_NOT_FOUND";
        public const string LINK_CREATED = "TICKET_LINK_CREATED";
        public const string LINK_REMOVED = "TICKET_LINK_REMOVED";

        /// <summary>AC-925.3. Resolving as Duplicate without a DuplicateOf link is a state conflict.</summary>
        public const string DUPLICATE_REQUIRES_LINK = "TICKET_DUPLICATE_REQUIRES_LINK";
```

`SystemCode.cs`:

```csharp
        public const string VAL078 = "VAL078"; // Link type invalid (AC-925.1)
        public const string VAL079 = "VAL079"; // Link target reference required (AC-925.1)

        public const string ERR081 = "ERR081"; // Link target ticket not found
        public const string ERR082 = "ERR082"; // Link already exists
        public const string ERR083 = "ERR083"; // Direct duplicate cycle (AC-925.2)
        public const string ERR084 = "ERR084"; // Self link
        public const string ERR085 = "ERR085"; // Duplicate resolution requires a DuplicateOf link (AC-925.3)
        public const string ERR086 = "ERR086"; // Link not found

        public const string CON077 = "CON077"; // Link created
        public const string CON078 = "CON078"; // Link removed
```

`SystemCodeMap.cs`:

```csharp
        ["TICKET_LINK_TYPE_INVALID"] = SystemCode.VAL078,
        ["TICKET_LINK_TARGET_REQUIRED"] = SystemCode.VAL079,
        ["TICKET_LINK_TARGET_NOT_FOUND"] = SystemCode.ERR081,
        ["TICKET_LINK_EXISTS"] = SystemCode.ERR082,
        ["TICKET_LINK_CYCLE"] = SystemCode.ERR083,
        ["TICKET_LINK_SELF"] = SystemCode.ERR084,
        ["TICKET_DUPLICATE_REQUIRES_LINK"] = SystemCode.ERR085,
        ["TICKET_LINK_NOT_FOUND"] = SystemCode.ERR086,
        ["TICKET_LINK_CREATED"] = SystemCode.CON077,
        ["TICKET_LINK_REMOVED"] = SystemCode.CON078,
```

`Resources.yaml`:

```yaml
TICKET_LINK_TYPE_INVALID:
  ar: "نوع الربط يجب أن يكون RelatedTo أو DuplicateOf"
  en: "Link type must be RelatedTo or DuplicateOf"

TICKET_LINK_TARGET_REQUIRED:
  ar: "مرجع التذكرة المستهدفة مطلوب"
  en: "A target ticket reference is required"

TICKET_LINK_TARGET_NOT_FOUND:
  ar: "لا توجد تذكرة بهذا المرجع"
  en: "No ticket exists with that reference"

TICKET_LINK_EXISTS:
  ar: "هذا الربط موجود بالفعل"
  en: "This link already exists"

TICKET_LINK_CYCLE:
  ar: "لا يمكن أن تكون التذكرتان نسختين مكررتين من بعضهما"
  en: "Two tickets cannot be duplicates of each other"

TICKET_LINK_SELF:
  ar: "لا يمكن ربط التذكرة بنفسها"
  en: "A ticket cannot be linked to itself"

TICKET_DUPLICATE_REQUIRES_LINK:
  ar: "حل التذكرة كنسخة مكررة يتطلب ربطها أولاً بالتذكرة الأصلية"
  en: "Resolving as a duplicate requires a DuplicateOf link to the original ticket"

TICKET_LINK_NOT_FOUND:
  ar: "الربط غير موجود"
  en: "The link does not exist"

TICKET_LINK_CREATED:
  ar: "تم إنشاء الربط"
  en: "Link created"

TICKET_LINK_REMOVED:
  ar: "تمت إزالة الربط"
  en: "Link removed"
```

- [ ] **Step 6: Write the failing integration tests**

Create `backend/tests/CustomerSupport.Tests/Integration/TicketLinkEndpointTests.cs` (fixture
skeleton as in Tasks 1–3; the resolve helper posts Task 1's resolution fields):

```csharp
using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>US-925 — links on the wire, and the Duplicate-code rule they gate (AC-925.1/2/3/4/5).</summary>
public class TicketLinkEndpointTests : IAsyncLifetime
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
            email = $"links-{Guid.NewGuid():N}@example.com",
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    public Task DisposeAsync()
    {
        _supervisor.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<(Guid Id, string Reference)> CreateTicketAsync()
    {
        var response = await _supervisor.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot sign in",
            description = "The portal rejects my password.",
            customerId = _customerId,
            categoryId = _categoryId,
            impact = "Medium",
            urgency = "Medium",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
        var detail = await DetailAsync(id);
        return (id, detail.Reference);
    }

    private async Task<LinkedDetail> DetailAsync(Guid id) =>
        (await _supervisor.GetFromJsonAsync<Response<LinkedDetail>>($"/api/Tickets/{id}"))!.Data!;

    private Task<HttpResponseMessage> LinkAsync(Guid id, string linkType, string targetReference) =>
        _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/links", new { linkType, targetReference });

    private async Task<HttpResponseMessage> ResolveAsync(Guid id, string resolutionCode)
    {
        var detail = await DetailAsync(id);
        if (detail.Status == "New")
        {
            (await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/status",
                new { status = "Open", rowVersion = detail.RowVersion })).StatusCode.Should().Be(HttpStatusCode.OK);
            detail = await DetailAsync(id);
        }

        return await _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/status", new
        {
            status = "Resolved",
            rowVersion = detail.RowVersion,
            resolutionCode,
            resolutionNotes = "Consolidated into the original ticket.",
        });
    }

    [Fact]
    [Trait("AC", "925.1")]
    [Trait("AC", "925.5")]
    public async Task A_Link_Is_Created_And_Visible_From_Both_Sides()
    {
        var (a, _) = await CreateTicketAsync();
        var (b, refB) = await CreateTicketAsync();

        var response = await LinkAsync(a, "RelatedTo", refB);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DetailAsync(a)).Links.Should().ContainSingle(l =>
            l.LinkType == "RelatedTo" && l.Direction == "Outbound" && l.OtherReference == refB);
        (await DetailAsync(b)).Links.Should().ContainSingle(l =>
            l.LinkType == "RelatedTo" && l.Direction == "Inbound");
    }

    [Fact]
    [Trait("AC", "925.1")]
    public async Task An_Unknown_Target_Reference_Is_A_400_On_The_Field()
    {
        var (a, _) = await CreateTicketAsync();

        var response = await LinkAsync(a, "RelatedTo", "TKT-999999");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "TargetReference");
    }

    [Fact]
    [Trait("AC", "925.1")]
    public async Task A_Self_Link_Is_A_400()
    {
        var (a, refA) = await CreateTicketAsync();

        var response = await LinkAsync(a, "RelatedTo", refA);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("AC", "925.1")]
    public async Task The_Same_Link_Twice_Is_A_409()
    {
        var (a, _) = await CreateTicketAsync();
        var (_, refB) = await CreateTicketAsync();
        (await LinkAsync(a, "RelatedTo", refB)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await LinkAsync(a, "RelatedTo", refB);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("AC", "925.2")]
    public async Task A_Direct_Duplicate_Cycle_Is_A_409()
    {
        var (a, refA) = await CreateTicketAsync();
        var (b, refB) = await CreateTicketAsync();
        (await LinkAsync(a, "DuplicateOf", refB)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await LinkAsync(b, "DuplicateOf", refA);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("AC", "925.3")]
    public async Task Resolving_As_Duplicate_Without_A_Link_Is_A_409_And_With_One_Succeeds()
    {
        var (a, _) = await CreateTicketAsync();
        var (_, refB) = await CreateTicketAsync();

        (await ResolveAsync(a, "Duplicate")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await LinkAsync(a, "DuplicateOf", refB)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ResolveAsync(a, "Duplicate")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("AC", "925.4")]
    public async Task A_Link_Can_Be_Removed_And_A_Missing_One_Is_A_404()
    {
        var (a, _) = await CreateTicketAsync();
        var (_, refB) = await CreateTicketAsync();
        (await LinkAsync(a, "RelatedTo", refB)).StatusCode.Should().Be(HttpStatusCode.OK);
        var linkId = (await DetailAsync(a)).Links.Single().Id;

        (await _supervisor.DeleteAsync($"/api/Tickets/{a}/links/{linkId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await DetailAsync(a)).Links.Should().BeEmpty();
        (await _supervisor.DeleteAsync($"/api/Tickets/{a}/links/{linkId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record LinkedDetail(
        Guid Id, string Reference, string Status, string RowVersion, IReadOnlyList<LinkRow> Links);

    private sealed record LinkRow(
        Guid Id, string LinkType, string Direction, Guid OtherTicketId, string OtherReference, string OtherSubject);
}
```

- [ ] **Step 7: Run to verify failure**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketLinkEndpointTests"
```

Expected: FAIL — `/links` routes 404, `Links` absent, Duplicate resolves without a link.

- [ ] **Step 8: Implement application + API**

`AddTicketLinkCommand.cs`:

```csharp
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketLink;

/// <summary>Links this ticket to another by its reference (US-925, AC-925.1).</summary>
public record AddTicketLinkCommand(Guid TicketId, string LinkType, string TargetReference)
    : ICommand<Response<Guid>>;

/// <summary>The add-link payload. The target is addressed by its TKT-nnnnnn reference.</summary>
public record AddTicketLinkRequest(string LinkType, string TargetReference);
```

`AddTicketLinkCommandValidator.cs`:

```csharp
using CustomerSupport.Application.Errors;
using CustomerSupport.Domain.ValueObjects;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketLink;

public class AddTicketLinkCommandValidator : AbstractValidator<AddTicketLinkCommand>
{
    public AddTicketLinkCommandValidator()
    {
        RuleFor(x => x.LinkType)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_LINK_TYPE_INVALID)
            .Must(v => TicketLinkType.TryCreate(v, out _, out _))
            .WithErrorCode(ApplicationErrors.Validation.TICKET_LINK_TYPE_INVALID);

        RuleFor(x => x.TargetReference)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_LINK_TARGET_REQUIRED);
    }
}
```

`AddTicketLinkCommandHandler.cs`:

```csharp
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketLink;

/// <summary>
/// US-925. The cross-ticket guards live here — the entity cannot see other tickets. An unknown
/// target reference is a field-keyed 400 (the collection exists, the payload is wrong — the AC-31
/// reasoning); an existing row or a direct duplicate cycle is a 409 (well-formed, state is wrong).
/// </summary>
public class AddTicketLinkCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketLink> links,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<AddTicketLinkCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(AddTicketLinkCommand request, CancellationToken ct)
    {
        var source = await tickets.GetByIdAsync(request.TicketId, ct);
        if (source is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var target = await tickets.FirstOrDefaultAsync(
            t => t.Reference == request.TargetReference.Trim(), ct);
        if (target is null)
        {
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR,
                [new FieldError("TargetReference", SystemCodeMap.Resolve(ApplicationErrors.Ticket.LINK_TARGET_NOT_FOUND), ApplicationErrors.Ticket.LINK_TARGET_NOT_FOUND)]);
        }

        if (target.Id == source.Id)
        {
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR,
                [new FieldError("TargetReference", SystemCodeMap.Resolve(ApplicationErrors.Ticket.LINK_SELF), ApplicationErrors.Ticket.LINK_SELF)]);
        }

        var linkType = request.LinkType.Trim();

        if (await links.ExistsAsync(l =>
                l.SourceTicketId == source.Id && l.TargetTicketId == target.Id && l.LinkType == linkType, ct))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.LINK_EXISTS, MessageType.Conflict);
        }

        // AC-925.2 / spec A7: only the direct two-ticket cycle is refused; longer chains are legal.
        if (linkType == "DuplicateOf" && await links.ExistsAsync(l =>
                l.SourceTicketId == target.Id && l.TargetTicketId == source.Id && l.LinkType == "DuplicateOf", ct))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.LINK_CYCLE, MessageType.Conflict);
        }

        var link = TicketLink.Create(source.Id, target.Id, linkType, userContext.UserId);
        await links.AddAsync(link, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(link.Id, ApplicationErrors.Ticket.LINK_CREATED);
    }
}
```

`RemoveTicketLinkCommand.cs` (+ handler in the same folder):

```csharp
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.RemoveTicketLink;

/// <summary>Removes one link by id (US-925, AC-925.4). Either endpoint of the link may remove it.</summary>
public record RemoveTicketLinkCommand(Guid TicketId, Guid LinkId) : ICommand<Response<Guid>>;
```

```csharp
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Commands.RemoveTicketLink;

/// <summary>
/// AC-925.4. Removing a link from a ticket already resolved as Duplicate is allowed — the
/// resolution stands; history is not rewritten.
/// </summary>
public class RemoveTicketLinkCommandHandler(
    IRepository<TicketLink> links,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<RemoveTicketLinkCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(RemoveTicketLinkCommand request, CancellationToken ct)
    {
        var link = await links.FirstOrDefaultAsync(
            l => l.Id == request.LinkId
                && (l.SourceTicketId == request.TicketId || l.TargetTicketId == request.TicketId),
            ct);

        if (link is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.LINK_NOT_FOUND);
        }

        links.Remove(link);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(link.Id, ApplicationErrors.Ticket.LINK_REMOVED);
    }
}
```

`ChangeTicketStatusCommandHandler.cs` — AC-925.3. Inject `IRepository<TicketLink> links` in the
primary constructor, and insert **before** the `ticket.ChangeStatus(...)` call (after Task 1's
`resolution` construction):

```csharp
        // AC-925.3 / spec A8: "Duplicate" is a claim about another ticket — it must be backed by a
        // DuplicateOf link. A state check, not a shape check, so it is a 409 here and not in the
        // validator (which cannot see the link table).
        if (resolution?.Code == "Duplicate" && !await links.ExistsAsync(
                l => l.SourceTicketId == ticket.Id && l.LinkType == "DuplicateOf", ct))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.DUPLICATE_REQUIRES_LINK, MessageType.Conflict);
        }
```

`TicketDtos.cs` — add the DTO record after `TicketHistoryDto`:

```csharp
/// <summary>
/// One edge of the link graph as seen from the requested ticket (US-925, AC-925.5).
/// <c>Direction</c> is "Outbound" when the requested ticket is the source ("duplicate of …"),
/// "Inbound" when it is the target ("duplicated by …" / related-from).
/// </summary>
public record TicketLinkDto(
    Guid Id,
    string LinkType,
    string Direction,
    Guid OtherTicketId,
    string OtherReference,
    string OtherSubject);
```

and append to `TicketDetailDto` (after Task 3's `Tags`):

```csharp
    // US-925 / AC-925.5.
    IReadOnlyList<TicketLinkDto> Links);
```

`GetTicketByIdQueryHandler.cs` — inject `IRepository<TicketLink> links`; before the DTO
construction:

```csharp
        var linkRows = await links.ListAsync(
            l => l.SourceTicketId == ticket.Id || l.TargetTicketId == ticket.Id, ct);

        var otherIds = linkRows
            .Select(l => l.SourceTicketId == ticket.Id ? l.TargetTicketId : l.SourceTicketId)
            .Distinct()
            .ToList();
        var otherTickets = await tickets.ListAsync(t => otherIds.Contains(t.Id), ct);
        var otherMap = otherTickets.ToDictionary(t => t.Id);

        var linkDtos = linkRows.Select(l =>
        {
            var outbound = l.SourceTicketId == ticket.Id;
            var otherId = outbound ? l.TargetTicketId : l.SourceTicketId;
            var other = otherMap.GetValueOrDefault(otherId);
            return new TicketLinkDto(
                l.Id,
                l.LinkType,
                outbound ? "Outbound" : "Inbound",
                otherId,
                other?.Reference ?? string.Empty,
                other?.Subject ?? string.Empty);
        }).ToList();
```

final DTO argument: `linkDtos`.

`TicketsController.cs` — two endpoints after the tag endpoints (route base is `/api/tickets/{id}/links`;
note the existing `/{id:guid}/content/{contentId:guid}` KB-article routes at lines 267-294 are a
different sub-resource and are untouched):

```csharp
    /// <summary>Links this ticket to another by reference — RelatedTo or DuplicateOf (US-925).</summary>
    /// <remarks>
    /// An unknown reference is a 400 keyed to <c>targetReference</c>; the same link twice, or two
    /// tickets each DuplicateOf the other, is a 409. Creating a link never resolves anything —
    /// AC-925.3's rule runs on the status endpoint, at resolve time.
    /// </remarks>
    [HttpPost("{id:guid}/links")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddLink(Guid id, [FromBody] AddTicketLinkRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AddTicketLinkCommand(id, request.LinkType, request.TargetReference), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Removes a ticket link by id (US-925).</summary>
    [HttpDelete("{id:guid}/links/{linkId:guid}")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveLink(Guid id, Guid linkId, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveTicketLinkCommand(id, linkId), ct);
        return this.ToActionResult(result);
    }
```

`TicketLinkConfiguration.cs`:

```csharp
using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class TicketLinkConfiguration : IEntityTypeConfiguration<TicketLink>
{
    public void Configure(EntityTypeBuilder<TicketLink> builder)
    {
        builder.ToTable("TicketLinks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LinkType).HasMaxLength(16).IsRequired();

        // One row per (source, target, type) — the database backs the handler's 409 (AC-925.1).
        builder.HasIndex(x => new { x.SourceTicketId, x.TargetTicketId, x.LinkType })
            .IsUnique()
            .HasDatabaseName("UX_TicketLinks_Source_Target_Type");

        builder.HasIndex(x => x.TargetTicketId).HasDatabaseName("IX_TicketLinks_TargetTicketId");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.SourceTicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TargetTicketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 9: Migration**

```bash
dotnet ef migrations add AddTicketLinks --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi
```

Inspect: one `CreateTable("TicketLinks")` with the unique index and two restrict FKs; nothing else.

- [ ] **Step 10: Run the integration tests, then the full suite**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketLinkEndpointTests"
cd backend && dotnet test CustomerSupport.slnx
```

Expected: PASS / green. Task 1's resolution tests that resolve with `"Fixed"`/`"Workaround"` are
unaffected by the new 409 (it fires only for `"Duplicate"`).

- [ ] **Step 11: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat: related/duplicate ticket links, and Duplicate resolution requires one (AC-925.1..5)"
```
