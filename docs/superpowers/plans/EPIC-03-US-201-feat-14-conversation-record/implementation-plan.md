# Conversation Record (FEAT-14, backend) Implementation Plan

> **Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.**

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an agent record a message against a ticket (a phone call, an email logged manually, an
internal note about contact) and read them back as an ordered timeline, so the ticket's communication
history is complete — `US-201`, `AC-101`–`AC-109`.

**Architecture:** One new `BaseEntity`-derived entity (`TicketMessage`), append-only via a
generalised version of the existing `TicketHistory` guard (a new `IAppendOnlyEntity` marker
interface both types implement). One command (`RecordTicketMessageCommand`), one query
(`GetTicketMessagesQuery`, unpaginated), two new routes on the existing `TicketsController`. No
change to the envelope, the error-mapping pipeline, or any existing entity's shape.

**Tech Stack:** .NET 10, EF Core, MediatR (CQRS), FluentValidation, xUnit + `WebApplicationFactory`
against real LocalDB (this codebase never mocks the database — see `CrmApiFactory`).

**Spec:** [`docs/superpowers/specs/EPIC-03-US-201-conversation-record.md`](../../specs/EPIC-03-US-201-conversation-record.md)

## Global Constraints

- `Direction` ∈ `{"Inbound", "Outbound"}`, `Channel` ∈ `{"Email", "System"}` — exact strings, matching
  how `Ticket.Status`/`Ticket.Priority` are stored as validated strings, not enums, elsewhere in this
  codebase.
- `SenderId` is **always** the acting agent (`IUserContext.UserId`), never read from the request body
  — same non-negotiable rule `CustomerNote.AuthorId` follows (A1 in the spec).
- `Body` required, ≤4000 chars. `Subject` optional, ≤200 chars when present.
- Every new error code needs an `ar`/`en` pair in `Resources.yaml`, or
  `EveryErrorCode_HasABilingualMessage` fails the build (existing test, do not skip it).
- FluentValidation rules go directly against properties (`RuleFor(x => x.Body)`), never through an
  invoked `Func` — the `FEAT-03`/`MVP-05` lesson about losing `PropertyName`.
- All integration tests run against real LocalDB via `CrmApiFactory`, never the in-memory provider —
  this project's own rule, because ordering and FK-constraint criteria need a real engine.

---

### Task 1: `IAppendOnlyEntity` and the generalised guard

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Common/IAppendOnlyEntity.cs`
- Modify: `backend/src/CustomerSupport.Domain/Entities/Tickets/TicketHistory.cs:12` (class declaration)
- Modify: `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs:51-63` (`GuardAppendOnlyHistory`)
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketLifecycleEndpointTests.cs` (existing `AC49_*` tests — must still pass unmodified, proving the generalisation is behavior-preserving)

**Interfaces:**
- Produces: `IAppendOnlyEntity` (marker interface, no members) in `CustomerSupport.Domain.Common`,
  for `TicketMessage` (Task 2) to implement.

- [ ] **Step 1: Write the marker interface**

```csharp
// backend/src/CustomerSupport.Domain/Common/IAppendOnlyEntity.cs
namespace CustomerSupport.Domain.Common;

/// <summary>
/// Marks an entity whose rows may only ever be inserted, never updated or soft/hard-deleted —
/// enforced by <c>AppDbContext</c>'s <c>SaveChanges</c> guard (ADR-0010). A row that must never
/// change once written (a history entry, a recorded message) implements this instead of the guard
/// being told about the concrete type by name.
/// </summary>
public interface IAppendOnlyEntity
{
}
```

- [ ] **Step 2: Make `TicketHistory` implement it**

Edit `backend/src/CustomerSupport.Domain/Entities/Tickets/TicketHistory.cs`. The file currently has
one `using` (`CustomerSupport.Domain.ValueObjects`) — `BaseEntity` resolves without a `using` because
it lives in `CustomerSupport.Domain.Entities`, the namespace directly enclosing this file's own
`CustomerSupport.Domain.Entities.Tickets`. `IAppendOnlyEntity` lives in `CustomerSupport.Domain.Common`,
a sibling namespace, not an enclosing one, so it needs an explicit `using`. Add it, then change the
class declaration:

```csharp
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Domain.Entities.Tickets;

// ...

public class TicketHistory : BaseEntity, IAppendOnlyEntity
```

- [ ] **Step 3: Run the existing append-only tests to confirm the interface alone changes nothing yet**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC49"`
Expected: PASS (2 tests) — implementing an unused marker interface cannot change runtime behavior.

- [ ] **Step 4: Generalise the guard**

Edit `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs`. Replace:

```csharp
    private void GuardAppendOnlyHistory()
    {
        foreach (var entry in ChangeTracker.Entries<TicketHistory>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    $"Ticket history is append-only: a TicketHistory row (Id {entry.Entity.Id}) was " +
                    $"{entry.State.ToString().ToLowerInvariant()} in this unit of work. " +
                    "Append a new row instead of altering the record of what happened.");
            }
        }
    }
```

with:

```csharp
    private void GuardAppendOnlyHistory()
    {
        foreach (var entry in ChangeTracker.Entries<IAppendOnlyEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} is append-only: row (Id {((BaseEntity)entry.Entity).Id}) " +
                    $"was {entry.State.ToString().ToLowerInvariant()} in this unit of work. " +
                    "Append a new row instead of altering the record of what happened.");
            }
        }
    }
```

Add `using CustomerSupport.Domain.Common;` to `AppDbContext.cs` if not already present (it already
imports `CustomerSupport.Domain.Entities`, check before duplicating).

- [ ] **Step 5: Run the existing append-only tests again — same tests, now exercising the generalised guard**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC49"`
Expected: PASS (2 tests). The assertion `WithMessage("*append-only*")` still matches — the new
message still contains the word "append-only".

- [ ] **Step 6: Full build to confirm nothing else referenced the old private method's exact type filter**

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: Build succeeded, 0 errors, 0 new warnings.

- [ ] **Step 7: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Common/IAppendOnlyEntity.cs \
        backend/src/CustomerSupport.Domain/Entities/Tickets/TicketHistory.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs
git commit -m "refactor(tickets): generalise the append-only guard behind IAppendOnlyEntity"
```

---

### Task 2: The `TicketMessage` entity, its EF configuration and migration

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Tickets/TicketMessage.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketMessageConfiguration.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs` (add `DbSet<TicketMessage>`)
- Test: `backend/tests/CustomerSupport.Tests/Unit/TicketMessageTests.cs` (new)
- Migration: generated, not hand-written

**Interfaces:**
- Consumes: `IAppendOnlyEntity` (Task 1), `BaseEntity`.
- Produces: `TicketMessage.Create(Guid ticketId, string direction, string channel, string? subject, string body, Guid senderId)` → `TicketMessage`, for Task 3's command handler.
  Public read-only properties: `TicketId`, `Direction`, `Channel`, `Subject`, `Body`, `SenderId`, `SentAt` (all `Guid`/`string`/`string?`/`DateTime` as named).

- [ ] **Step 1: Write the failing unit test for the entity's invariants**

```csharp
// backend/tests/CustomerSupport.Tests/Unit/TicketMessageTests.cs
using CustomerSupport.Domain.Entities.Tickets;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit;

public class TicketMessageTests
{
    private static readonly Guid TicketId = Guid.NewGuid();
    private static readonly Guid SenderId = Guid.NewGuid();

    [Fact]
    public void Create_ValidFields_StoresThemAndStampsSentAt()
    {
        var before = DateTime.UtcNow;

        var message = TicketMessage.Create(TicketId, "Outbound", "System", "Follow-up", "Called back.", SenderId);

        message.TicketId.Should().Be(TicketId);
        message.Direction.Should().Be("Outbound");
        message.Channel.Should().Be("System");
        message.Subject.Should().Be("Follow-up");
        message.Body.Should().Be("Called back.");
        message.SenderId.Should().Be(SenderId);
        message.SentAt.Should().BeOnOrAfter(before);
        message.Id.Should().Be(Guid.Empty); // unassigned — EF generates it, same reasoning as TicketHistory.Record
    }

    [Fact]
    public void Create_NoSubject_IsAllowed()
    {
        var message = TicketMessage.Create(TicketId, "Inbound", "Email", null, "Customer called.", SenderId);

        message.Subject.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyBody_Throws(string body)
    {
        var act = () => TicketMessage.Create(TicketId, "Outbound", "System", null, body, SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("body");
    }

    [Fact]
    public void Create_BodyOverMaxLength_Throws()
    {
        var act = () => TicketMessage.Create(TicketId, "Outbound", "System", null, new string('a', 4001), SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("body");
    }

    [Fact]
    public void Create_SubjectOverMaxLength_Throws()
    {
        var act = () => TicketMessage.Create(TicketId, "Outbound", "System", new string('a', 201), "Body", SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("subject");
    }

    [Theory]
    [InlineData("Sideways")]
    [InlineData("")]
    public void Create_UnrecognisedDirection_Throws(string direction)
    {
        var act = () => TicketMessage.Create(TicketId, direction, "System", null, "Body", SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("direction");
    }

    [Theory]
    [InlineData("Carrier Pigeon")]
    [InlineData("")]
    public void Create_UnrecognisedChannel_Throws(string channel)
    {
        var act = () => TicketMessage.Create(TicketId, "Outbound", channel, null, "Body", SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("channel");
    }

    [Fact]
    public void Create_EmptySenderId_Throws()
    {
        var act = () => TicketMessage.Create(TicketId, "Outbound", "System", null, "Body", Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("senderId");
    }

    [Fact]
    public void Create_EmptyTicketId_Throws()
    {
        var act = () => TicketMessage.Create(Guid.Empty, "Outbound", "System", null, "Body", SenderId);

        act.Should().Throw<ArgumentException>().WithParameterName("ticketId");
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketMessageTests"`
Expected: FAIL — `TicketMessage` does not exist (compile error), or once it exists as a stub, assertion failures.

- [ ] **Step 3: Write the entity**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Tickets/TicketMessage.cs
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Entities.Tickets;

/// <summary>
/// One recorded communication against a ticket — a phone call, an email logged manually, an
/// internal note about contact made (AC-101). Distinct from <see cref="TicketHistory"/>: history
/// records *what happened to the ticket* (status, assignment); this records *what was said*.
///
/// <see cref="SenderId"/> is always the acting agent, even when <see cref="Direction"/> is
/// "Inbound" — customers have no login in this platform, so an inbound message this sprint means
/// an agent logging what a customer said, not a customer-authored record (spec A1).
/// </summary>
public class TicketMessage : BaseEntity, IAppendOnlyEntity
{
    private static readonly string[] AllowedDirections = ["Inbound", "Outbound"];
    private static readonly string[] AllowedChannels = ["Email", "System"];

    public Guid TicketId { get; private set; }
    public string Direction { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Guid SenderId { get; private set; }
    public DateTime SentAt { get; private set; }

    public static TicketMessage Create(
        Guid ticketId, string direction, string channel, string? subject, string body, Guid senderId)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A ticket is required", nameof(ticketId));
        }

        if (!AllowedDirections.Contains(direction))
        {
            throw new ArgumentException($"Direction must be one of: {string.Join(", ", AllowedDirections)}", nameof(direction));
        }

        if (!AllowedChannels.Contains(channel))
        {
            throw new ArgumentException($"Channel must be one of: {string.Join(", ", AllowedChannels)}", nameof(channel));
        }

        if (subject is { Length: > 200 })
        {
            throw new ArgumentException("Subject must not exceed 200 characters", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Body is required", nameof(body));
        }

        if (body.Length > 4000)
        {
            throw new ArgumentException("Body must not exceed 4000 characters", nameof(body));
        }

        if (senderId == Guid.Empty)
        {
            throw new ArgumentException("A sender is required", nameof(senderId));
        }

        return new TicketMessage
        {
            // Id deliberately unassigned — see TicketHistory.Record for why: a client-assigned Guid
            // on a row appended to an already-tracked Ticket makes EF mark it Modified, and the
            // append-only guard then refuses a perfectly legitimate append.
            TicketId = ticketId,
            Direction = direction,
            Channel = channel,
            Subject = subject,
            Body = body.Trim(),
            SenderId = senderId,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = senderId
        };
    }
}
```

- [ ] **Step 4: Run the unit test again to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketMessageTests"`
Expected: PASS (9 tests).

- [ ] **Step 5: EF configuration**

```csharp
// backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketMessageConfiguration.cs
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Direction).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200);
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.SentAt).IsRequired();

        // Oldest-first timeline read (AC-106).
        builder.HasIndex(x => new { x.TicketId, x.SentAt })
            .HasDatabaseName("IX_TicketMessages_Ticket_SentAt");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 6: Register the `DbSet`**

Edit `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs`, add beside the
`TicketHistory` DbSet:

```csharp
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
```

Add `using CustomerSupport.Domain.Entities.Tickets;` if not already present (it already is, for
`Ticket`/`TicketHistory`).

- [ ] **Step 7: Generate the migration**

Run:
```
cd backend && dotnet ef migrations add AddTicketMessages --project src/CustomerSupport.Infrastructure --startup-project src/CustomerSupport.InternalApi
```
Expected: a new migration file adding the `TicketMessages` table, two FKs, and the
`IX_TicketMessages_Ticket_SentAt` index. **Read the generated migration before committing it** — a
generated `Up()` that also touches an unrelated table means the model snapshot had drifted and needs
investigating before this is trusted.

- [ ] **Step 8: Build and run the full unit suite**

Run: `cd backend && dotnet build CustomerSupport.slnx && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketMessageTests|FullyQualifiedName~AC49"`
Expected: Build succeeded, 0 errors. All 11 tests pass.

- [ ] **Step 9: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Tickets/TicketMessage.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/TicketMessageConfiguration.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs \
        backend/src/CustomerSupport.Infrastructure/Migrations/ \
        backend/tests/CustomerSupport.Tests/Unit/TicketMessageTests.cs
git commit -m "feat(tickets): add the TicketMessage entity, its configuration and migration"
```

---

### Task 3: Record a message — command, validator, endpoint, write-side tests

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommand.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommandHandler.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommandValidator.cs`
- Modify: `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs` (add codes)
- Modify: `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml` (add bilingual entries)
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (add `POST {id}/messages`)
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketMessagesEndpointTests.cs` (new — write-side cases in this task, read-side cases added in Task 4)

**Interfaces:**
- Consumes: `TicketMessage.Create(...)` (Task 2), `IRepository<Ticket>`, `IRepository<TicketMessage>`, `IUnitOfWork`, `IUserContext.UserId`, `IMessageFactory`.
- Produces: `RecordTicketMessageCommand(Guid TicketId, string Direction, string Channel, string? Subject, string Body) : ICommand<Response<Guid>>` and `RecordTicketMessageRequest(string Direction, string Channel, string? Subject, string Body)`, for the controller and Task 4's tests to reuse.

- [ ] **Step 1: Write the failing integration tests**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/TicketMessagesEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// FEAT-14 — recording and reading messages against a ticket. `AC-101` through `AC-109`.
/// Real LocalDB, same reasoning as every other endpoint suite here: FK constraints and ordering
/// criteria are not provable against the in-memory provider.
/// </summary>
public class TicketMessagesEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _callerId;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        var (client, caller) = await _factory.CreateAuthenticatedClientAsync();
        _client = client;
        _callerId = caller.Id;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    /// <summary>A ticket of this test's own — creates its own customer and category first.</summary>
    private async Task<Guid> CreateTicketAsync()
    {
        var customer = await _client.PostAsJsonAsync("/api/Customers", new
        {
            name = "Nadia Farouk",
            email = $"messages-{Guid.NewGuid():N}@example.com",
            phone = (string?)null,
        });
        var customerId = (await customer.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;

        var categories = await _client.GetFromJsonAsync<Response<List<CategoryRow>>>("/api/Categories");
        var categoryId = categories!.Data!.First().Id;

        var ticket = await _client.PostAsJsonAsync("/api/Tickets", new
        {
            subject = "Cannot log in",
            description = "Password reset link never arrives.",
            customerId,
            categoryId,
            priority = "Normal",
        });

        return (await ticket.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    private Task<HttpResponseMessage> RecordMessageAsync(Guid ticketId, object body) =>
        _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/messages", body);

    private async Task<List<TicketMessageRow>> GetMessagesAsync(Guid ticketId)
    {
        var response = await _client.GetFromJsonAsync<Response<List<TicketMessageRow>>>(
            $"/api/Tickets/{ticketId}/messages");
        return response!.Data!;
    }

    // --- AC-101 — recording a message ----------------------------------------------------------

    [Fact]
    [Trait("AC", "101")]
    public async Task AC101_RecordMessage_ValidFields_Returns201AndIsReadable()
    {
        var ticketId = await CreateTicketAsync();

        var response = await RecordMessageAsync(ticketId, new
        {
            direction = "Outbound",
            channel = "System",
            subject = "Follow-up",
            body = "Called the customer back.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        created!.Data.Should().NotBeEmpty();

        var messages = await GetMessagesAsync(ticketId);
        var stored = messages.Single();
        stored.Id.Should().Be(created.Data);
        stored.Direction.Should().Be("Outbound");
        stored.Channel.Should().Be("System");
        stored.Subject.Should().Be("Follow-up");
        stored.Body.Should().Be("Called the customer back.");
        stored.SenderId.Should().Be(_callerId);
    }

    [Fact]
    [Trait("AC", "101")]
    public async Task AC101_RecordMessage_NoSubject_IsAllowed()
    {
        var ticketId = await CreateTicketAsync();

        var response = await RecordMessageAsync(ticketId, new
        {
            direction = "Inbound",
            channel = "Email",
            body = "Customer emailed to say the issue is resolved.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetMessagesAsync(ticketId)).Single().Subject.Should().BeNull();
    }

    // --- AC-102 — empty body ---------------------------------------------------------------------

    [Fact]
    [Trait("AC", "102")]
    public async Task AC102_RecordMessage_EmptyBody_Returns400KeyedToBody()
    {
        var ticketId = await CreateTicketAsync();

        var response = await RecordMessageAsync(ticketId, new { direction = "Outbound", channel = "System", body = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == "Body");
        (await GetMessagesAsync(ticketId)).Should().BeEmpty();
    }

    // --- AC-103 — unknown ticket ------------------------------------------------------------------

    [Fact]
    [Trait("AC", "103")]
    public async Task AC103_RecordMessage_UnknownTicket_Returns404()
    {
        var response = await RecordMessageAsync(Guid.NewGuid(), new { direction = "Outbound", channel = "System", body = "Nobody's ticket." });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- AC-104 — invalid Direction/Channel ---------------------------------------------------------

    [Theory]
    [Trait("AC", "104")]
    [InlineData("Sideways", "System", "Direction")]
    [InlineData("Outbound", "Carrier Pigeon", "Channel")]
    public async Task AC104_RecordMessage_InvalidDirectionOrChannel_Returns400KeyedToField(
        string direction, string channel, string expectedField)
    {
        var ticketId = await CreateTicketAsync();

        var response = await RecordMessageAsync(ticketId, new { direction, channel, body = "Body text." });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Errors.Should().Contain(e => e.Field == expectedField);
    }

    // --- AC-105 — authentication ----------------------------------------------------------------

    [Fact]
    [Trait("AC", "105")]
    public async Task AC105_RecordMessage_WithoutAToken_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            $"/api/Tickets/{Guid.NewGuid()}/messages",
            new { direction = "Outbound", channel = "System", body = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("AC", "105")]
    public async Task AC105_GetMessages_WithoutAToken_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/Tickets/{Guid.NewGuid()}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- AC-109 — append-only, same proof as TicketHistory (AC-49) --------------------------------

    [Fact]
    [Trait("AC", "109")]
    public async Task AC109_UpdatingAMessageRow_IsRefused()
    {
        var ticketId = await CreateTicketAsync();
        (await RecordMessageAsync(ticketId, new { direction = "Outbound", channel = "System", body = "Original." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.TicketMessages.FirstAsync(m => m.TicketId == ticketId);

        db.Entry(row).Property(m => m.Body).CurrentValue = "Tampered";

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }

    [Fact]
    [Trait("AC", "109")]
    public async Task AC109_DeletingAMessageRow_IsRefused()
    {
        var ticketId = await CreateTicketAsync();
        (await RecordMessageAsync(ticketId, new { direction = "Outbound", channel = "System", body = "Original." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.TicketMessages.FirstAsync(m => m.TicketId == ticketId);

        db.TicketMessages.Remove(row);

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }

    public sealed record CategoryRow(Guid Id, string Name);

    public sealed record TicketMessageRow(
        Guid Id, string Direction, string Channel, string? Subject, string Body,
        Guid SenderId, string SenderName, DateTime SentAt);
}
```

- [ ] **Step 2: Run to verify the suite fails to compile (the command/endpoint don't exist yet)**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketMessagesEndpointTests"`
Expected: FAIL — compile error, route/type not found.

- [ ] **Step 3: Add the error codes**

Edit `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs`. Inside `Ticket`, add
after `ASSIGNED`:

```csharp
        /// <summary>AC-101.</summary>
        public const string MESSAGE_RECORDED = "TICKET_MESSAGE_RECORDED";
```

Inside `Validation`, add a new labeled group after the `TICKET_STATUS_INVALID` block:

```csharp
        // Ticket messages — FEAT-14, AC-101..AC-104.
        public const string MESSAGE_BODY_REQUIRED = "MESSAGE_BODY_REQUIRED";
        public const string MESSAGE_BODY_MAX_LENGTH = "MESSAGE_BODY_MAX_LENGTH";
        public const string MESSAGE_SUBJECT_MAX_LENGTH = "MESSAGE_SUBJECT_MAX_LENGTH";
        public const string MESSAGE_DIRECTION_INVALID = "MESSAGE_DIRECTION_INVALID";
        public const string MESSAGE_CHANNEL_INVALID = "MESSAGE_CHANNEL_INVALID";
```

- [ ] **Step 4: Add the bilingual resource entries**

Edit `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml`, append:

```yaml
TICKET_MESSAGE_RECORDED:
  ar: "تم تسجيل الرسالة بنجاح"
  en: "Message recorded successfully"

MESSAGE_BODY_REQUIRED:
  ar: "نص الرسالة مطلوب"
  en: "The message text is required"

MESSAGE_BODY_MAX_LENGTH:
  ar: "يجب ألا تتجاوز الرسالة 4000 حرف"
  en: "The message must not exceed 4000 characters"

MESSAGE_SUBJECT_MAX_LENGTH:
  ar: "يجب ألا يتجاوز الموضوع 200 حرف"
  en: "The subject must not exceed 200 characters"

MESSAGE_DIRECTION_INVALID:
  ar: "اتجاه الرسالة غير صالح"
  en: "The message direction is not valid"

MESSAGE_CHANNEL_INVALID:
  ar: "قناة الرسالة غير صالحة"
  en: "The message channel is not valid"
```

- [ ] **Step 5: Write the command**

```csharp
// backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommand.cs
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.RecordTicketMessage;

/// <summary>Records a message against a ticket — AC-101.</summary>
public record RecordTicketMessageCommand(Guid TicketId, string Direction, string Channel, string? Subject, string Body)
    : ICommand<Response<Guid>>;

/// <summary>The record-message payload. No SenderId — the handler takes it from the session (spec A1).</summary>
public record RecordTicketMessageRequest(string Direction, string Channel, string? Subject, string Body);
```

- [ ] **Step 6: Write the validator**

```csharp
// backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommandValidator.cs
using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.RecordTicketMessage;

public class RecordTicketMessageCommandValidator : AbstractValidator<RecordTicketMessageCommand>
{
    private static readonly string[] AllowedDirections = ["Inbound", "Outbound"];
    private static readonly string[] AllowedChannels = ["Email", "System"];

    public RecordTicketMessageCommandValidator()
    {
        RuleFor(x => x.Direction)
            .Must(d => AllowedDirections.Contains(d))
            .WithErrorCode(ApplicationErrors.Validation.MESSAGE_DIRECTION_INVALID);

        RuleFor(x => x.Channel)
            .Must(c => AllowedChannels.Contains(c))
            .WithErrorCode(ApplicationErrors.Validation.MESSAGE_CHANNEL_INVALID);

        RuleFor(x => x.Subject)
            .MaximumLength(200).WithErrorCode(ApplicationErrors.Validation.MESSAGE_SUBJECT_MAX_LENGTH)
            .When(x => x.Subject is not null);

        RuleFor(x => x.Body)
            .NotEmpty().WithErrorCode(ApplicationErrors.Validation.MESSAGE_BODY_REQUIRED)
            .MaximumLength(4000).WithErrorCode(ApplicationErrors.Validation.MESSAGE_BODY_MAX_LENGTH);
    }
}
```

- [ ] **Step 7: Write the handler**

```csharp
// backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/RecordTicketMessageCommandHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Commands.RecordTicketMessage;

public class RecordTicketMessageCommandHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketMessage> messages,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IMessageFactory messageFactory)
    : ICommandHandler<RecordTicketMessageCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(RecordTicketMessageCommand request, CancellationToken ct)
    {
        if (!await tickets.ExistsAsync(t => t.Id == request.TicketId, ct))
        {
            return messageFactory.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var message = TicketMessage.Create(
            request.TicketId, request.Direction, request.Channel, request.Subject, request.Body, userContext.UserId);

        await messages.AddAsync(message, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messageFactory.Success(message.Id, ApplicationErrors.Ticket.MESSAGE_RECORDED);
    }
}
```

- [ ] **Step 8: Add the controller endpoint**

Edit `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs`. Add
`using CustomerSupport.Application.Features.Tickets.Commands.RecordTicketMessage;` to the usings,
and this action after `Assign`:

```csharp
    /// <summary>Records a message against a ticket — a call, a manually-logged email, a note about contact (AC-101).</summary>
    /// <remarks>
    /// The sender is taken from the session, never from the payload — there is no sender field on
    /// the request record for a client to fill in. <c>Direction</c> distinguishes what the agent is
    /// recording (something the customer said vs. something the agent said); it does not change who
    /// <c>SenderId</c> is, which is always the caller (spec A1).
    /// </remarks>
    /// <param name="id">The ticket the message belongs to.</param>
    /// <param name="request">Direction, channel, optional subject, and the message body.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordMessage(Guid id, [FromBody] RecordTicketMessageRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RecordTicketMessageCommand(id, request.Direction, request.Channel, request.Subject, request.Body),
            ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        // Location points at the list, not at the message — same reasoning as CustomerNotes:
        // there is no single-message route, and AC-106 reads the timeline as a whole.
        return CreatedAtAction(nameof(GetMessages), new { id }, result);
    }
```

**Do not add `GetMessages` yet** — it is Task 4. This step will not compile until Task 4 adds it;
that is expected and is resolved by Task 4's first step, not by stubbing it here.

- [ ] **Step 9: Run only the write-side tests (the read-side ones need Task 4 and will still fail to compile)**

This task cannot fully compile until `GetMessages` exists (the `CreatedAtAction(nameof(GetMessages), ...)`
reference and the test file's `GetMessagesAsync` helper both need it). Proceed directly to Task 4
before running the suite — record this task's tests as written-but-unverified until Task 4 completes,
and verify all of `AC-101`–`AC-109` together at the end of Task 4's Step 6.

- [ ] **Step 10: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Commands/RecordTicketMessage/ \
        backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs \
        backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml \
        backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/TicketMessagesEndpointTests.cs
git commit -m "feat(tickets): record a message against a ticket (AC-101..AC-105, AC-109)"
```

(This commit will not build in isolation — `GetMessages` does not exist yet. If the executor's
workflow requires each commit to build, merge this commit with Task 4's instead of committing here.)

---

### Task 4: Read the message timeline — query, endpoint, read-side tests

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketMessages/GetTicketMessagesQuery.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketMessages/GetTicketMessagesQueryHandler.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Dtos/TicketMessageDto.cs`
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (add `GET {id}/messages`)
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketMessagesEndpointTests.cs` (append read-side cases)

**Interfaces:**
- Consumes: `IRepository<TicketMessage>.ListOrderedAsync(...)`, `IIdentityUserService.FindByIdAsync`, `IRepository<Ticket>.ExistsAsync`.
- Produces: `GetTicketMessagesQuery(Guid TicketId) : IQuery<Response<IReadOnlyList<TicketMessageDto>>>`, `TicketMessageDto`, consumed by the controller and (later) the frontend plan.

- [ ] **Step 1: Append the read-side tests to `TicketMessagesEndpointTests.cs`**

```csharp
    // --- AC-106 — ordering and sender names --------------------------------------------------------

    [Fact]
    [Trait("AC", "106")]
    public async Task AC106_GetMessages_ReturnsOldestFirstWithSenderNames()
    {
        var ticketId = await CreateTicketAsync();

        (await RecordMessageAsync(ticketId, new { direction = "Outbound", channel = "System", body = "First." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // A real gap, or both SentAt stamps can land inside one tick and the order assertion below
        // proves nothing — the same reasoning CustomerNotesEndpointTests uses.
        await Task.Delay(20);

        (await RecordMessageAsync(ticketId, new { direction = "Inbound", channel = "Email", body = "Second." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var messages = await GetMessagesAsync(ticketId);

        messages.Should().HaveCount(2);
        messages.Select(m => m.Body).Should().ContainInOrder("First.", "Second.");
        messages.Should().BeInAscendingOrder(m => m.SentAt);
        messages.Should().OnlyContain(m => m.SenderName == "Test User");
        messages.Should().OnlyContain(m => m.SenderId != Guid.Empty);
    }

    // --- AC-107 — unknown ticket ------------------------------------------------------------------

    [Fact]
    [Trait("AC", "107")]
    public async Task AC107_GetMessages_UnknownTicket_Returns404()
    {
        var response = await _client.GetAsync($"/api/Tickets/{Guid.NewGuid()}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- AC-108 — empty timeline is 200, not 404 -----------------------------------------------------

    [Fact]
    [Trait("AC", "108")]
    public async Task AC108_GetMessages_NoMessagesYet_ReturnsEmptyListNot404()
    {
        var ticketId = await CreateTicketAsync();

        var response = await _client.GetAsync($"/api/Tickets/{ticketId}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetMessagesAsync(ticketId)).Should().BeEmpty();
    }
```

- [ ] **Step 2: Run the full file to verify it still fails to compile (query/endpoint absent)**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketMessagesEndpointTests"`
Expected: FAIL — compile error, `GetMessages` not found on the controller.

- [ ] **Step 3: Write the DTO**

```csharp
// backend/src/CustomerSupport.Application/Features/Tickets/Dtos/TicketMessageDto.cs
namespace CustomerSupport.Application.Features.Tickets.Dtos;

/// <summary>One entry of a ticket's message timeline (AC-106). SenderName is resolved at read
/// time from SenderId, the same arrangement TicketHistory's actor names and CustomerNote's author
/// names use — the row stores no name.</summary>
public record TicketMessageDto(
    Guid Id, string Direction, string Channel, string? Subject, string Body,
    Guid SenderId, string SenderName, DateTime SentAt);
```

- [ ] **Step 4: Write the query and its handler**

```csharp
// backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketMessages/GetTicketMessagesQuery.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Tickets.Dtos;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketMessages;

/// <summary>A ticket's message timeline, oldest first — AC-106. Unpaginated, like TicketHistory: a
/// timeline renders in full on one screen (spec A6).</summary>
public record GetTicketMessagesQuery(Guid TicketId) : IQuery<Response<IReadOnlyList<TicketMessageDto>>>;
```

```csharp
// backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketMessages/GetTicketMessagesQueryHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Tickets.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketMessages;

public class GetTicketMessagesQueryHandler(
    IRepository<Ticket> tickets,
    IRepository<TicketMessage> messages,
    IIdentityUserService identityUsers,
    IMessageFactory messageFactory)
    : IQueryHandler<GetTicketMessagesQuery, Response<IReadOnlyList<TicketMessageDto>>>
{
    public async Task<Response<IReadOnlyList<TicketMessageDto>>> Handle(GetTicketMessagesQuery request, CancellationToken ct)
    {
        if (!await tickets.ExistsAsync(t => t.Id == request.TicketId, ct))
        {
            return messageFactory.NotFound<IReadOnlyList<TicketMessageDto>>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var rows = await messages.ListOrderedAsync(
            m => m.TicketId == request.TicketId,
            m => m.SentAt,
            descending: false,
            ct);

        var senderNames = new Dictionary<Guid, string>();
        foreach (var senderId in rows.Select(m => m.SenderId).Distinct())
        {
            var sender = await identityUsers.FindByIdAsync(senderId, ct);
            senderNames[senderId] = sender?.FullName ?? string.Empty;
        }

        IReadOnlyList<TicketMessageDto> items = rows.Select(m => new TicketMessageDto(
            m.Id, m.Direction, m.Channel, m.Subject, m.Body,
            m.SenderId, senderNames.GetValueOrDefault(m.SenderId, string.Empty), m.SentAt)).ToList();

        return messageFactory.Success(items, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
```

`ApplicationErrors.General.SUCCESS_OPERATION` is the constant `GetTicketByIdQueryHandler` uses for
its own success return — this handler's closest sibling — so it is used verbatim above rather than
the `SystemCodeMap.Resolve("SUCCESS_OPERATION")` spelling some list-query handlers use.

- [ ] **Step 5: Add the controller endpoint**

Edit `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs`. Add
`using CustomerSupport.Application.Features.Tickets.Queries.GetTicketMessages;` to the usings
(`CustomerSupport.Application.Features.Tickets.Dtos` is already imported — the controller already
uses `TicketDetailDto` from it), and this action right before `RecordMessage` (so the route that names it in `CreatedAtAction` reads top-to-bottom):

```csharp
    /// <summary>A ticket's message timeline, oldest first (AC-106).</summary>
    /// <remarks>
    /// Unpaginated — the same shape the status-history timeline takes, because this is meant to
    /// render in full on one screen (spec A6). An unknown ticket is 404; a ticket with no recorded
    /// messages is 200 with an empty list, not 404 (AC-108).
    /// </remarks>
    /// <param name="id">The ticket identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType(typeof(Response<IReadOnlyList<TicketMessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<IReadOnlyList<TicketMessageDto>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTicketMessagesQuery(id), ct);
        return this.ToActionResult(result);
    }
```

- [ ] **Step 6: Run the full new test file, then the full suite**

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: Build succeeded, 0 errors, 0 new warnings.

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketMessagesEndpointTests|FullyQualifiedName~TicketMessageTests|FullyQualifiedName~AC49"`
Expected: PASS — all `TicketMessagesEndpointTests` (11), `TicketMessageTests` (9), and the two `AC49_*`
regression tests.

Run: `cd backend && dotnet test CustomerSupport.slnx`
Expected: PASS, full suite, no regressions. Paste the actual summary line
(`Passed! - Failed: 0, Passed: N, Skipped: 0, Total: N`) into this task's record — do not claim this
without having run it.

- [ ] **Step 7: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketMessages/ \
        backend/src/CustomerSupport.Application/Features/Tickets/Dtos/TicketMessageDto.cs \
        backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/TicketMessagesEndpointTests.cs
git commit -m "feat(tickets): read a ticket's message timeline (AC-106..AC-108)"
```

---

## Definition of done

`AC-101` through `AC-109` each covered by a test naming it (via `[Trait("AC", "n")]`) · `dotnet build`
clean, 0 new warnings · `dotnet test CustomerSupport.slnx` green, full output pasted into the task
record · migration reviewed before commit · task records written in `tasks/` as each task completes,
per this project's `sdd-workflow` convention · frontend plan (`US-202`) written next, immediately —
not deferred to a later sprint.
