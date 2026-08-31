# Task 3 — Ticket tags (US-924, AC-924.1…AC-924.4)

**Cut order note:** this slice is cut *after* Task 4 but *before* Tasks 1–2 — if the sprint is out
of time here, stop and record the cut in `docs/assessment/rubric-traceability.md`.

**Files:**
- Create: `backend/src/CustomerSupport.Domain/ValueObjects/TagValue.cs`
- Create: `backend/src/CustomerSupport.Domain/Entities/Tickets/TicketTag.cs` (standalone child entity — the `TicketNote.cs` pattern, handler-orchestrated, because `IRepository.GetTrackedAsync` cannot include child collections)
- Modify: `backend/src/CustomerSupport.Domain/ValueObjects/TicketChangeType.cs` (add `TagAdded`, `TagRemoved`)
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/AddTicketTag/AddTicketTagCommand.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/AddTicketTag/AddTicketTagCommandValidator.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/AddTicketTag/AddTicketTagCommandHandler.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/RemoveTicketTag/RemoveTicketTagCommand.cs` (+ handler, no validator — the route carries the value)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQuery.cs` (add `Tag`)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQueryHandler.cs` (filter + per-row tags)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQueryHandler.cs` (detail tags)
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Dtos/TicketDtos.cs` (both DTOs, appended)
- Modify: `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs`, `Messages/SystemCode.cs`, `Messages/SystemCodeMap.cs`
- Modify: `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketTagConfiguration.cs`
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (two endpoints)
- Test: `backend/tests/CustomerSupport.Tests/Unit/Domain/TagValueTests.cs` (new)
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketTagEndpointTests.cs` (new)

**Interfaces:**
- Consumes: Task 2's create contract (`impact`/`urgency` fixtures); `TicketHistory.Record(Guid ticketId, Guid actorId, TicketChangeType changeType, string? fromValue, string? toValue)`
  (the factory `Ticket.Append` at `Ticket.cs:490-493` calls — confirm the exact parameter list in
  `TicketHistory.cs` before use); `IRepository<T>.ListProjectedAsync` (confirm signature at
  `backend/src/CustomerSupport.Domain/Interfaces/IRepository.cs:20`).
- Produces:
  - `static class TagValue { const int MaxLength = 30; const int MaxPerTicket = 10; static string Normalize(string) }`
  - `class TicketTag : BaseEntity { Guid TicketId; string Value; static TicketTag Create(Guid ticketId, string rawValue, Guid createdBy) }` — `Create` normalizes via `TagValue.Normalize`
  - `TicketChangeType.TagAdded` (`"TagAdded"`), `TicketChangeType.TagRemoved` (`"TagRemoved"`)
  - `GET /api/tickets?tag=` filter; `POST /api/tickets/{id}/tags`; `DELETE /api/tickets/{id}/tags/{value}`
  - Detail DTO appends `IReadOnlyList<string> Tags`; list DTO appends `IReadOnlyList<string> Tags`.

## Steps

- [ ] **Step 1: Write the failing normalization tests**

Create `backend/tests/CustomerSupport.Tests/Unit/Domain/TagValueTests.cs`:

```csharp
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

/// <summary>US-924 / AC-924.1/2 — one normalization rule, stated once (spec A6).</summary>
public class TagValueTests
{
    [Theory]
    [Trait("AC", "924.1")]
    [InlineData("  Billing  ", "billing")]
    [InlineData("VIP   Customer", "vip customer")]
    [InlineData("password-reset", "password-reset")]
    [InlineData("BILLING", "billing")]
    public void Normalizes_Trim_Collapse_And_Case(string raw, string expected)
    {
        TagValue.Normalize(raw).Should().Be(expected);
    }

    [Fact]
    [Trait("AC", "924.2")]
    public void Arabic_Tags_Survive_Normalization()
    {
        TagValue.Normalize(" فوترة ").Should().Be("فوترة");
    }

    [Theory]
    [Trait("AC", "924.1")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("tag!")]
    [InlineData("tag_underscore")]
    [InlineData("semi;colon")]
    public void Refuses_Empty_And_Forbidden_Characters(string raw)
    {
        var act = () => TagValue.Normalize(raw);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "924.1")]
    public void Refuses_Values_Over_30_Chars_After_Normalization()
    {
        var act = () => TagValue.Normalize(new string('a', 31));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("AC", "924.1")]
    public void Accepts_Exactly_30_Chars()
    {
        TagValue.Normalize(new string('a', 30)).Should().HaveLength(30);
    }

    [Fact]
    [Trait("AC", "924.1")]
    public void TicketTag_Create_Stores_The_Normalized_Value()
    {
        var ticketId = Guid.NewGuid();
        var actor = Guid.NewGuid();

        var tag = TicketTag.Create(ticketId, "  Billing ISSUE ", actor);

        tag.TicketId.Should().Be(ticketId);
        tag.Value.Should().Be("billing issue");
        tag.CreatedBy.Should().Be(actor);
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TagValueTests"
```

Expected: compile error — `TagValue` does not exist.

- [ ] **Step 3: Implement the domain**

`backend/src/CustomerSupport.Domain/ValueObjects/TagValue.cs`:

```csharp
namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// The tag normalization rule (US-924, spec A6), stated once: trim, collapse internal whitespace,
/// invariant lowercase; 1–30 chars; Unicode letters (Arabic included), digits, dash and space.
/// A static rule rather than a wrapping value object because the persisted thing is the entity
/// (<c>TicketTag</c>) — this is the rule it must pass through, not a second identity.
/// </summary>
public static class TagValue
{
    public const int MaxLength = 30;
    public const int MaxPerTicket = 10;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A tag value is required", nameof(value));
        }

        var collapsed = string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var normalized = collapsed.ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            throw new ArgumentException($"A tag must not exceed {MaxLength} characters", nameof(value));
        }

        if (!normalized.All(c => char.IsLetterOrDigit(c) || c is '-' or ' '))
        {
            throw new ArgumentException("A tag may contain only letters, digits, dashes and spaces", nameof(value));
        }

        return normalized;
    }
}
```

`backend/src/CustomerSupport.Domain/Entities/Tickets/TicketTag.cs` — the `TicketNote.cs` shape:

```csharp
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Domain.Entities.Tickets;

/// <summary>
/// A free-form label on a ticket (US-924). Normalized at the door — a value that never passed
/// <see cref="TagValue.Normalize"/> cannot exist as a row.
/// </summary>
public class TicketTag : BaseEntity
{
    public Guid TicketId { get; private set; }
    public string Value { get; private set; } = string.Empty;

    public static TicketTag Create(Guid ticketId, string rawValue, Guid createdBy)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A ticket is required", nameof(ticketId));
        }

        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(createdBy));
        }

        return new TicketTag
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Value = TagValue.Normalize(rawValue),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }
}
```

(Check `TicketNote.cs:1` — if `BaseEntity` needs no using there, it needs none here either.)

`TicketChangeType.cs` — add after `Reprioritized` (Task 2), extend `All` and the `Create` switch
and its error message:

```csharp
    public static readonly TicketChangeType TagAdded = new("TagAdded");
    public static readonly TicketChangeType TagRemoved = new("TagRemoved");
```

- [ ] **Step 4: Run the unit tests, commit the domain slice**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TagValueTests"
git add backend/src/CustomerSupport.Domain backend/tests/CustomerSupport.Tests/Unit/Domain/TagValueTests.cs
git commit -m "feat: tag normalization rule and TicketTag entity (AC-924.1..2)"
```

Expected: PASS (all TagValueTests).

- [ ] **Step 5: Message codes — all four registrations**

`ApplicationErrors.cs` `Validation` (after Task 2's consts):

```csharp
        // US-924 — tags (AC-924.1).
        public const string TICKET_TAG_INVALID = "TICKET_TAG_INVALID";
        public const string TICKET_TAG_DUPLICATE = "TICKET_TAG_DUPLICATE";
        public const string TICKET_TAG_LIMIT = "TICKET_TAG_LIMIT";
```

`ApplicationErrors.cs` `Ticket` (after `RECLASSIFIED`):

```csharp
        // US-924.
        public const string TAG_ADDED = "TICKET_TAG_ADDED";
        public const string TAG_REMOVED = "TICKET_TAG_REMOVED";
        public const string TAG_NOT_FOUND = "TICKET_TAG_NOT_FOUND";
```

`SystemCode.cs`:

```csharp
        public const string VAL075 = "VAL075"; // Tag invalid (AC-924.1)
        public const string VAL076 = "VAL076"; // Tag duplicate (AC-924.1)
        public const string VAL077 = "VAL077"; // Tag limit reached (AC-924.1)

        public const string ERR080 = "ERR080"; // Tag not found on ticket

        public const string CON075 = "CON075"; // Tag added
        public const string CON076 = "CON076"; // Tag removed
```

`SystemCodeMap.cs`:

```csharp
        ["TICKET_TAG_INVALID"] = SystemCode.VAL075,
        ["TICKET_TAG_DUPLICATE"] = SystemCode.VAL076,
        ["TICKET_TAG_LIMIT"] = SystemCode.VAL077,
        ["TICKET_TAG_NOT_FOUND"] = SystemCode.ERR080,
        ["TICKET_TAG_ADDED"] = SystemCode.CON075,
        ["TICKET_TAG_REMOVED"] = SystemCode.CON076,
```

`Resources.yaml`:

```yaml
TICKET_TAG_INVALID:
  ar: "الوسم يجب أن يتكون من 1 إلى 30 حرفاً: أحرف أو أرقام أو شرطات أو مسافات"
  en: "A tag must be 1-30 characters: letters, digits, dashes or spaces"

TICKET_TAG_DUPLICATE:
  ar: "هذا الوسم موجود بالفعل على التذكرة"
  en: "The ticket already carries this tag"

TICKET_TAG_LIMIT:
  ar: "لا يمكن أن تحمل التذكرة أكثر من 10 وسوم"
  en: "A ticket cannot carry more than 10 tags"

TICKET_TAG_NOT_FOUND:
  ar: "الوسم غير موجود على هذه التذكرة"
  en: "The ticket does not carry this tag"

TICKET_TAG_ADDED:
  ar: "تمت إضافة الوسم"
  en: "Tag added"

TICKET_TAG_REMOVED:
  ar: "تمت إزالة الوسم"
  en: "Tag removed"
```

- [ ] **Step 6: Write the failing integration tests**

Create `backend/tests/CustomerSupport.Tests/Integration/TicketTagEndpointTests.cs` (fixture
skeleton as in Tasks 1–2; create payload uses `impact`/`urgency` per Task 2):

```csharp
using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>US-924 — tags on the wire (AC-924.1/2/3/4).</summary>
public class TicketTagEndpointTests : IAsyncLifetime
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
            email = $"tags-{Guid.NewGuid():N}@example.com",
        });
        _customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    public Task DisposeAsync()
    {
        _supervisor.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateTicketAsync()
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
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data!;
    }

    private Task<HttpResponseMessage> AddTagAsync(Guid id, string value) =>
        _supervisor.PostAsJsonAsync($"/api/Tickets/{id}/tags", new { value });

    private async Task<TaggedDetail> DetailAsync(Guid id) =>
        (await _supervisor.GetFromJsonAsync<Response<TaggedDetail>>($"/api/Tickets/{id}"))!.Data!;

    [Fact]
    [Trait("AC", "924.1")]
    [Trait("AC", "924.3")]
    public async Task Adding_A_Tag_Normalizes_Lists_And_Records_History()
    {
        var id = await CreateTicketAsync();

        var response = await AddTagAsync(id, "  Billing ISSUE ");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await DetailAsync(id);
        detail.Tags.Should().ContainSingle().Which.Should().Be("billing issue");
        detail.History.Should().Contain(h => h.ChangeType == "TagAdded" && h.ToValue == "billing issue");
    }

    [Fact]
    [Trait("AC", "924.2")]
    public async Task An_Arabic_Tag_Round_Trips_Intact()
    {
        var id = await CreateTicketAsync();

        (await AddTagAsync(id, "فوترة")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await DetailAsync(id)).Tags.Should().Contain("فوترة");
    }

    [Fact]
    [Trait("AC", "924.1")]
    public async Task A_Duplicate_Tag_Is_A_400_On_The_Value_Field()
    {
        var id = await CreateTicketAsync();
        (await AddTagAsync(id, "billing")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await AddTagAsync(id, " BILLING ");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        body!.Errors.Should().Contain(e => e.Field == "Value");
    }

    [Fact]
    [Trait("AC", "924.1")]
    public async Task The_Eleventh_Tag_Is_A_400()
    {
        var id = await CreateTicketAsync();
        for (var i = 1; i <= 10; i++)
        {
            (await AddTagAsync(id, $"tag-{i}")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var response = await AddTagAsync(id, "tag-11");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("AC", "924.1")]
    [Trait("AC", "924.3")]
    public async Task Removing_A_Tag_Deletes_It_And_Records_History()
    {
        var id = await CreateTicketAsync();
        (await AddTagAsync(id, "billing")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _supervisor.DeleteAsync($"/api/Tickets/{id}/tags/billing");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await DetailAsync(id);
        detail.Tags.Should().BeEmpty();
        detail.History.Should().Contain(h => h.ChangeType == "TagRemoved" && h.FromValue == "billing");
    }

    [Fact]
    [Trait("AC", "924.1")]
    public async Task Removing_A_Missing_Tag_Is_A_404()
    {
        var id = await CreateTicketAsync();

        var response = await _supervisor.DeleteAsync($"/api/Tickets/{id}/tags/nothing");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("AC", "924.4")]
    public async Task The_Queue_Filters_By_Tag_Server_Side()
    {
        var tagged = await CreateTicketAsync();
        var untagged = await CreateTicketAsync();
        var marker = $"queue-{Guid.NewGuid():N}"[..20];
        (await AddTagAsync(tagged, marker)).StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await _supervisor.GetFromJsonAsync<Response<PagedTickets>>($"/api/Tickets?tag={marker}");

        page!.Data!.Items.Should().ContainSingle(t => t.Id == tagged);
        page.Data.Items.Should().NotContain(t => t.Id == untagged);
    }

    private sealed record TaggedDetail(Guid Id, IReadOnlyList<string> Tags, IReadOnlyList<HistoryRow> History);
    private sealed record HistoryRow(string ChangeType, string? FromValue, string? ToValue);
    private sealed record PagedTickets(IReadOnlyList<Row> Items);
    private sealed record Row(Guid Id, IReadOnlyList<string> Tags);
}
```

> `PaginatedList<T>`'s JSON property for rows — confirm against an existing queue test
> (`TicketEndpointTests`) whether it is `items` and adjust the `PagedTickets` record to match.

- [ ] **Step 7: Run to verify failure**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketTagEndpointTests"
```

Expected: FAIL — `/tags` routes 404, DTO has no `Tags`.

- [ ] **Step 8: Implement application + API**

`AddTicketTagCommand.cs`:

```csharp
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketTag;

/// <summary>Adds one tag to a ticket (US-924, AC-924.1).</summary>
public record AddTicketTagCommand(Guid TicketId, string Value) : ICommand<Response<Guid>>;

/// <summary>The add-tag payload. The raw value — normalization is the domain's.</summary>
public record AddTicketTagRequest(string Value);
```

`AddTicketTagCommandValidator.cs`:

```csharp
using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketTag;

/// <summary>
/// Shape only: present and not absurdly long (2× the normalized cap, since collapsing may shrink
/// it). Charset/length precision lives in <c>TagValue.Normalize</c>, surfaced by the handler as
/// the same field-keyed 400.
/// </summary>
public class AddTicketTagCommandValidator : AbstractValidator<AddTicketTagCommand>
{
    public AddTicketTagCommandValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.TICKET_TAG_INVALID)
            .MaximumLength(60).WithErrorCode(ApplicationErrors.Validation.TICKET_TAG_INVALID);
    }
}
```

`AddTicketTagCommandHandler.cs`:

```csharp
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketTag;

/// <summary>
/// US-924. Orchestrated over the tag repository (the `TicketNote` pattern) because the ticket
/// aggregate is loaded without its child collections; the duplicate/limit checks therefore run
/// against the committed rows, and the history row is appended explicitly (AC-924.3).
/// </summary>
public class AddTicketTagCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketTag> ticketTags,
    IRepository<TicketHistory> history,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<AddTicketTagCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(AddTicketTagCommand request, CancellationToken ct)
    {
        if (!await tickets.ExistsAsync(t => t.Id == request.TicketId, ct))
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        string normalized;
        try
        {
            normalized = TagValue.Normalize(request.Value);
        }
        catch (ArgumentException)
        {
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR,
                [new FieldError("Value", SystemCodeMap.Resolve(ApplicationErrors.Validation.TICKET_TAG_INVALID), ApplicationErrors.Validation.TICKET_TAG_INVALID)]);
        }

        var existing = await ticketTags.ListAsync(t => t.TicketId == request.TicketId, ct);

        if (existing.Any(t => t.Value == normalized))
        {
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR,
                [new FieldError("Value", SystemCodeMap.Resolve(ApplicationErrors.Validation.TICKET_TAG_DUPLICATE), ApplicationErrors.Validation.TICKET_TAG_DUPLICATE)]);
        }

        if (existing.Count >= TagValue.MaxPerTicket)
        {
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR,
                [new FieldError("Value", SystemCodeMap.Resolve(ApplicationErrors.Validation.TICKET_TAG_LIMIT), ApplicationErrors.Validation.TICKET_TAG_LIMIT)]);
        }

        var tag = TicketTag.Create(request.TicketId, normalized, userContext.UserId);
        await ticketTags.AddAsync(tag, ct);
        await history.AddAsync(
            TicketHistory.Record(request.TicketId, userContext.UserId, TicketChangeType.TagAdded, null, normalized), ct);

        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(tag.Id, ApplicationErrors.Ticket.TAG_ADDED);
    }
}
```

(The `messages.Validation` + `FieldError` idiom is `CreateTicketCommandHandler.cs:29-44` — copy
its exact `FieldError` constructor usage. Confirm `TicketHistory.Record`'s parameter list in
`TicketHistory.cs` and adjust the call if it differs.)

`RemoveTicketTagCommand.cs` (+ handler in the same folder):

```csharp
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.RemoveTicketTag;

/// <summary>Removes one tag from a ticket (US-924). The value arrives via the route.</summary>
public record RemoveTicketTagCommand(Guid TicketId, string Value) : ICommand<Response<Guid>>;
```

```csharp
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Application.Features.Tickets.Commands.RemoveTicketTag;

public class RemoveTicketTagCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketTag> ticketTags,
    IRepository<TicketHistory> history,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<RemoveTicketTagCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(RemoveTicketTagCommand request, CancellationToken ct)
    {
        if (!await tickets.ExistsAsync(t => t.Id == request.TicketId, ct))
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        string normalized;
        try
        {
            normalized = TagValue.Normalize(request.Value);
        }
        catch (ArgumentException)
        {
            // A value that cannot be a tag cannot be on the ticket — same answer as absent.
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.TAG_NOT_FOUND);
        }

        var tag = await ticketTags.FirstOrDefaultAsync(
            t => t.TicketId == request.TicketId && t.Value == normalized, ct);

        if (tag is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.TAG_NOT_FOUND);
        }

        ticketTags.Remove(tag);
        await history.AddAsync(
            TicketHistory.Record(request.TicketId, userContext.UserId, TicketChangeType.TagRemoved, normalized, null), ct);

        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(tag.Id, ApplicationErrors.Ticket.TAG_REMOVED);
    }
}
```

`GetTicketsQuery.cs` — add after `Unassigned`:

```csharp
    /// <summary>US-924 / AC-924.4. Only tickets carrying this tag (normalized before matching).</summary>
    public string? Tag { get; init; }
```

`GetTicketsQueryHandler.cs` — inject `IRepository<TicketTag> ticketTags`; before the
`PredicateBuilder` block:

```csharp
        IReadOnlyList<Guid>? taggedTicketIds = null;
        if (!string.IsNullOrWhiteSpace(request.Tag))
        {
            var normalizedTag = string.Join(' ',
                request.Tag.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToLowerInvariant();
            taggedTicketIds = await ticketTags.ListProjectedAsync(
                g => g.Value == normalizedTag, g => g.TicketId, ct);
        }
```

add to the filter chain:

```csharp
            .WhereIf(taggedTicketIds is not null, t => taggedTicketIds!.Contains(t.Id))
```

and after `pagedTickets` is materialized, load the page's tags and pass them into the DTO:

```csharp
        var pageIds = pagedTickets.Select(t => t.Id).ToList();
        var pageTags = await ticketTags.ListAsync(g => pageIds.Contains(g.TicketId), ct);
        var tagMap = pageTags
            .GroupBy(g => g.TicketId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(x => x.Value).OrderBy(v => v)]);
```

with the `TicketListItemDto` construction gaining the final argument:

```csharp
            tagMap.GetValueOrDefault(t.Id, []),
```

`TicketDtos.cs` — append to `TicketListItemDto` (after Task 2's `Urgency`):

```csharp
    // US-924 / AC-924.4. Normalized values, alphabetical; empty when untagged.
    IReadOnlyList<string> Tags);
```

and to `TicketDetailDto` (after Task 2's `Urgency`):

```csharp
    // US-924. Normalized values, alphabetical.
    IReadOnlyList<string> Tags);
```

`GetTicketByIdQueryHandler.cs` — inject `IRepository<TicketTag> ticketTags`, load and append:

```csharp
        var tags = await ticketTags.ListAsync(g => g.TicketId == ticket.Id, ct);
```

final DTO argument: `[.. tags.Select(g => g.Value).OrderBy(v => v)]`.

`TicketsController.cs` — two endpoints after `Reclassify`:

```csharp
    /// <summary>Adds a tag to a ticket (US-924). Duplicates, an 11th tag, or a bad value are field-keyed 400s.</summary>
    [HttpPost("{id:guid}/tags")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTag(Guid id, [FromBody] AddTicketTagRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AddTicketTagCommand(id, request.Value), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Removes a tag by its normalized value (US-924).</summary>
    [HttpDelete("{id:guid}/tags/{value}")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTag(Guid id, string value, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveTicketTagCommand(id, value), ct);
        return this.ToActionResult(result);
    }
```

`GetAll` (line 63) gains `[FromQuery] string? tag = null` and passes `Tag = tag` into the query.

`TicketTagConfiguration.cs`:

```csharp
using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class TicketTagConfiguration : IEntityTypeConfiguration<TicketTag>
{
    public void Configure(EntityTypeBuilder<TicketTag> builder)
    {
        builder.ToTable("TicketTags");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Value).HasMaxLength(30).IsRequired();

        // One tag once per ticket (AC-924.1) — the database backs what the handler refuses.
        builder.HasIndex(x => new { x.TicketId, x.Value })
            .IsUnique()
            .HasDatabaseName("UX_TicketTags_TicketId_Value");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 9: Migration**

```bash
dotnet ef migrations add AddTicketTags --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi
```

Inspect: one `CreateTable("TicketTags")` with the unique index; nothing else.

- [ ] **Step 10: Run the integration tests, then the full suite**

```bash
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketTagEndpointTests"
cd backend && dotnet test CustomerSupport.slnx
```

Expected: PASS / green. The list-DTO change touches `GetTicketsQueryHandler` consumers — any
queue test asserting positional DTO shape will name itself here; fix by adding the `Tags` field to
its expectation, never by removing the field.

- [ ] **Step 11: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat: ticket tags with normalized values, history rows and a queue filter (AC-924.1..4)"
```
