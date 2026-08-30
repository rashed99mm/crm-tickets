# FEAT-21 · AI Assist — Stitch-Faithful Right Rail (Backend Payload Shape)

> **Spec:** `docs/superpowers/specs/EPIC-11-US-701-feat-21-stitch-right-rail.md` (AC-21.11..AC-21.16)
>
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tighten the `AiSuggestions.Payload` JSON shape for `Summary` and `Reply` kinds to carry
the new fields the Stitch right-rail mockups show — sentiment on the summary, an array of drafts
on the reply — without changing the HTTP surface, the error envelope, or any other endpoint.
No migration; `Payload` is `NVARCHAR(MAX)`.

**Architecture:** Add one port method to `IAiService` (`ClassifySentimentAsync`), one enum in
domain (`AiSentiment`), and update the two existing handlers to write the new shape. The provider
prompt in `ResilientAiService` switches to JSON for draft-reply so a single model call yields up
to three drafts. The `NoOp` provider returns `Fail` for sentiment — the handler treats that as
`null` and continues.

**Tech Stack:** .NET 10, xUnit, FluentAssertions, `WebApplicationFactory<CrmApiFactory>`.

---

### Task 1: Add `AiSentiment` enum to domain (AC-21.11)

**Files:**
- Modify: `backend/src/CustomerSupport.Domain/Entities/Ai/AiSuggestion.cs` (add nested enum)
- Modify: `backend/tests/CustomerSupport.Tests/Unit/Domain/AiSuggestionEntityTests.cs` *(new file)*

- [ ] **Step 1: Add the enum to `AiSuggestion`**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Ai/AiSuggestion.cs
public class AiSuggestion : BaseEntity
{
    /// <summary>AC-21.11 — the three sentiment labels the summary card surfaces. Stored as the
    /// string name in JSON; <c>null</c> means the model did not return a parseable label.</summary>
    public enum AiSentiment { Frustrated, Neutral, Satisfied }
    // ...existing members...
}
```

The enum lives in the domain because the *string values* it accepts are documented at the contract
boundary (`AiSuggestions.Payload.sentiment`). The handler maps model output to this enum, then
serialises the **string name** to JSON so the wire shape is stable across enum reorderings.

- [ ] **Step 2: Failing test first — the enum round-trips through the JSON the handler writes**

```csharp
// backend/tests/CustomerSupport.Tests/Unit/Domain/AiSuggestionEntityTests.cs
using System.Text.Json;
using CustomerSupport.Domain.Entities.Ai;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Domain;

public class AiSuggestionEntityTests
{
    [Fact]
    [Trait("AC", "21.11")]
    public void AiSentiment_SerialisesAsStringName()
    {
        var payload = JsonSerializer.Serialize(new
        {
            text = "Customer cannot sign in.",
            sentiment = AiSuggestion.AiSentiment.Frustrated.ToString(),
        });
        using var doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("sentiment").GetString().Should().Be("Frustrated");
    }

    [Fact]
    [Trait("AC", "21.11")]
    public void AiSentiment_Null_RoundtripsAsJsonNull()
    {
        string? sentiment = null;
        var payload = JsonSerializer.Serialize(new { text = "x", sentiment });
        using var doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("sentiment").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
```

- [ ] **Step 3: Build & test**

```powershell
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AiSuggestionEntityTests"
```

Expected: 2 passing.

- [ ] **Step 4: Commit**

```powershell
git add backend/src/CustomerSupport.Domain/Entities/Ai/AiSuggestion.cs backend/tests/CustomerSupport.Tests/Unit/Domain/AiSuggestionEntityTests.cs
git commit -m "feat(feat-21): add AiSentiment enum to domain"
```

---

### Task 2: Add `IAiService.ClassifySentimentAsync` (AC-21.11)

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Ai/IAiService.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/Ai/NoOpAiService.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/Ai/ResilientAiService.cs`

- [ ] **Step 1: Add the port method**

```csharp
// backend/src/CustomerSupport.Application/Ai/IAiService.cs
public interface IAiService
{
    // ...existing members...

    /// <summary>AC-21.11 / A5 — a single-word sentiment label. <c>Fail</c> is translated by
    /// the caller into <c>null</c>; the summary never fails on a sentiment error.</summary>
    Task<AiOutcome<string?>> ClassifySentimentAsync(string threadText, CancellationToken ct);
}
```

- [ ] **Step 2: NoOp implementation**

```csharp
// backend/src/CustomerSupport.Infrastructure/Ai/NoOpAiService.cs
public Task<AiOutcome<string?>> ClassifySentimentAsync(string threadText, CancellationToken ct) =>
    Task.FromResult(AiOutcome<string?>.Fail("AI assist is not configured"));
```

- [ ] **Step 3: Failing test for the new port method on `ResilientAiService`**

The `ResilientAiService` reads the existing `AiJson.ParseStringArray` and a similar strict single-token
parser. Add a `ParseSentiment` helper in `AiJson` and a test that mirrors `ValidItemsObject_ParsesStrings`
in `backend/tests/CustomerSupport.Tests/Unit/Ai/AiProviderAbstractionTests.cs:65-89`.

```csharp
// backend/src/CustomerSupport.Infrastructure/Ai/ResilientAiService.cs
public static partial class AiJson
{
    // ...existing ParseStringArray...

    public static string? ParseSentiment(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var items = doc.RootElement.GetProperty("items");
            if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0) return null;
            var label = items[0].GetString();
            return label switch
            {
                "Frustrated" or "Neutral" or "Satisfied" => label,
                _ => null,
            };
        }
        catch (JsonException) { return null; }
    }
}
```

Test:

```csharp
// backend/tests/CustomerSupport.Tests/Unit/Ai/AiProviderAbstractionTests.cs
[Fact]
public void ValidSentiment_ParsesLabel() =>
    AiJson.ParseSentiment("""{"items":["Frustrated"]}""").Should().Be("Frustrated");

[Fact]
public void UnknownSentiment_ReturnsNull() =>
    AiJson.ParseSentiment("""{"items":["Ecstatic"]}""").Should().BeNull();

[Fact]
public void GarbageSentiment_ReturnsNull() =>
    AiJson.ParseSentiment("not json at all").Should().BeNull();
```

- [ ] **Step 4: Implement `ClassifySentimentAsync` on `ResilientAiService`**

```csharp
public async Task<AiOutcome<string?>> ClassifySentimentAsync(string threadText, CancellationToken ct)
{
    var instruction = Arabic
        ? "صنّف مزاج العميل في محادثة الدعم التالية. أعد JSON فقط بالشكل {\"items\":[\"...\"]} وقيمتها واحدة من: Frustrated أو Neutral أو Satisfied."
        : "Classify the customer sentiment of the support thread below. Answer with JSON only, shaped {\"items\":[\"...\"]}, where the value is exactly one of: Frustrated, Neutral, or Satisfied.";

    var result = await CompleteAsync(instruction, Fenced(threadText), ct);
    if (!result.Success) return AiOutcome<string?>.Fail(result.Error!);
    return AiOutcome<string?>.Ok(AiJson.ParseSentiment(result.Value));
}
```

- [ ] **Step 5: Build & test**

```powershell
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AiProviderAbstractionTests|FullyQualifiedName~AiSuggestionEntityTests"
```

Expected: existing 11 + 3 new = 14 passing.

- [ ] **Step 6: Commit**

```powershell
git add backend/src/CustomerSupport.Application/Ai/IAiService.cs backend/src/CustomerSupport.Infrastructure/Ai/NoOpAiService.cs backend/src/CustomerSupport.Infrastructure/Ai/ResilientAiService.cs backend/tests/CustomerSupport.Tests/Unit/Ai/AiProviderAbstractionTests.cs
git commit -m "feat(feat-21): classify sentiment via iai port with strict json"
```

---

### Task 3: Update `SummariseTicketCommandHandler` to persist `{ text, sentiment }` (AC-21.11, AC-21.15)

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Features/Ai/AiFeatures.cs:67-120`

- [ ] **Step 1: Failing test — handler writes the new shape**

Create `backend/tests/CustomerSupport.Tests/Unit/Features/Ai/SummariseHandlerPayloadTests.cs`. Use a
custom `IAiService` stub (defined inline in the test file) that returns canned `(text, sentiment)`
values; the test asserts the persisted `AiSuggestion.Payload` and the returned DTO both carry
both fields.

```csharp
// backend/tests/CustomerSupport.Tests/Unit/Features/Ai/SummariseHandlerPayloadTests.cs
using System.Text.Json;
using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Ai;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Ai;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Ai;

public class SummariseHandlerPayloadTests
{
    [Fact]
    [Trait("AC", "21.11")]
    public async Task Handler_PersistsTextAndSentiment()
    {
        var ai = new StubAiService { SummaryResult = ("Customer cannot sign in.", "Frustrated") };
        var (handler, db, ticketId) = await ArrangeAsync(ai);

        var response = await handler.Handle(new SummariseTicketCommand(ticketId), CancellationToken.None);

        response.Success.Should().BeTrue();
        var row = await db.AiSuggestions.AsNoTracking().SingleAsync();
        var payload = JsonDocument.Parse(row.Payload).RootElement;
        payload.GetProperty("text").GetString().Should().Be("Customer cannot sign in.");
        payload.GetProperty("sentiment").GetString().Should().Be("Frustrated");
    }

    [Fact]
    [Trait("AC", "21.11")]
    public async Task Handler_SentimentFailure_StillSucceedsWithNullSentiment()
    {
        var ai = new StubAiService
        {
            SummaryResult = ("Customer cannot sign in.", null),
        };
        var (handler, db, ticketId) = await ArrangeAsync(ai);

        var response = await handler.Handle(new SummariseTicketCommand(ticketId), CancellationToken.None);

        response.Success.Should().BeTrue();
        var payload = JsonDocument.Parse((await db.AiSuggestions.AsNoTracking().SingleAsync()).Payload).RootElement;
        payload.GetProperty("sentiment").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private static async Task<(SummariseTicketCommandHandler, AppDbContext, Guid)> ArrangeAsync(StubAiService ai)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);

        var ticket = Ticket.Create("Subject", "Body", Guid.NewGuid(), Guid.NewGuid(), "Normal");
        await db.Tickets.AddAsync(ticket);
        await db.TicketMessages.AddRangeAsync(
            TicketMessage.CreateInbound(ticket.Id, "first", "Email"),
            TicketMessage.CreateInbound(ticket.Id, "second", "Email"),
            TicketMessage.CreateInbound(ticket.Id, "third", "Email"));
        await db.SaveChangesAsync();

        var factory = new TestMessageFactory();
        var user = new TestUserContext(Guid.NewGuid());
        var handler = new SummariseTicketCommandHandler(
            new InMemoryTicketRepository(db),
            new InMemoryMessageRepository(db),
            new InMemorySuggestionRepository(db),
            ai, user, factory);
        return (handler, db, ticket.Id);
    }

    private sealed class StubAiService : IAiService
    {
        public (string Text, string? Sentiment) SummaryResult = ("", null);
        public bool IsAvailable => true;
        public Task<AiOutcome<string>> SummariseAsync(string t, CancellationToken c) =>
            Task.FromResult(AiOutcome<string>.Ok(SummaryResult.Text));
        public Task<AiOutcome<string?>> ClassifySentimentAsync(string t, CancellationToken c) =>
            Task.FromResult(SummaryResult.Sentiment is null
                ? AiOutcome<string?>.Fail("sentiment unavailable")
                : AiOutcome<string?>.Ok(SummaryResult.Sentiment));
        public Task<AiOutcome<string>> DraftReplyAsync(string t, string? i, CancellationToken c) =>
            Task.FromResult(AiOutcome<string>.Ok("draft"));
        public Task<AiOutcome<IReadOnlyList<string>>> SuggestCategoriesAsync(string t, IReadOnlyList<string> n, CancellationToken c) =>
            Task.FromResult(AiOutcome<IReadOnlyList<string>>.Ok(n));
        public Task<AiOutcome<IReadOnlyList<KbCitation>>> SuggestSolutionsAsync(string q, IReadOnlyList<KbPassage> p, CancellationToken c) =>
            Task.FromResult(AiOutcome<IReadOnlyList<KbCitation>>.Ok([]));
        public Task<AiOutcome<string>> AnswerAsync(string q, IReadOnlyList<KbPassage> p, CancellationToken c) =>
            Task.FromResult(AiOutcome<string>.Ok("answer"));
    }
}
```

The in-memory repos are already in use elsewhere in the test project; search the unit test folder
for `InMemory.*Repository` and copy the pattern from the closest existing test. If they do not
exist, create them in `backend/tests/CustomerSupport.Tests/Unit/TestHelpers/` mirroring the
existing `IRepository<T>` interface.

- [ ] **Step 2: Update `SummariseTicketCommandHandler`**

```csharp
// backend/src/CustomerSupport.Application/Features/Ai/AiFeatures.cs:77-112
public async Task<Response<AiSuggestionDto>> Handle(SummariseTicketCommand request, CancellationToken ct)
{
    if (!ai.IsAvailable) return AiMapping.NotConfigured<AiSuggestionDto>(factory);

    if (await AiMapping.AuthorizeTicketAsync(tickets, request.TicketId, user, factory) is { } denied)
        return ToDto<AiSuggestionDto>(denied, factory);

    var thread = await messages.ListProjectedAsync(
        m => m.TicketId == request.TicketId, m => new { m.Body });
    if (thread.Count < MinimumThreadMessages)
        return factory.Fail<AiSuggestionDto>(
            ApplicationErrors.General.AI_THREAD_TOO_SHORT, MessageType.Validation);

    var summaryOutcome = await ai.SummariseAsync(
        string.Join("\n", thread.Select(m => "- " + m.Body)), ct);
    if (!summaryOutcome.Success)
        return AiMapping.ProviderFailed<AiSuggestionDto>(factory);

    // A5 — sentiment failure is silent; the summary still ships.
    var sentimentOutcome = await ai.ClassifySentimentAsync(
        string.Join("\n", thread.Select(m => "- " + m.Body)), ct);
    var sentiment = sentimentOutcome.Success ? sentimentOutcome.Value : null;
    if (sentiment is not null
        && sentiment is not ("Frustrated" or "Neutral" or "Satisfied"))
    {
        sentiment = null;
    }

    var payload = JsonSerializer.Serialize(new
    {
        text = summaryOutcome.Value,
        sentiment,
    });
    var suggestion = AiSuggestion.Create(
        request.TicketId, "Summary", payload, user.UserId);
    await suggestions.AddAsync(suggestion, ct);
    return factory.Success(AiMapping.ToDto(suggestion), "AI_SUMMARY_READY");
}
```

- [ ] **Step 3: Build & test**

```powershell
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~SummariseHandlerPayloadTests"
```

Expected: 2 passing.

- [ ] **Step 4: Commit**

```powershell
git add backend/src/CustomerSupport.Application/Features/Ai/AiFeatures.cs backend/tests/CustomerSupport.Tests/Unit/Features/Ai/SummariseHandlerPayloadTests.cs
git commit -m "feat(feat-21): summarise handler persists text and sentiment"
```

---

### Task 4: Update `DraftReplyCommandHandler` to return `{ drafts: [...] }` (AC-21.12, AC-21.15)

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Features/Ai/AiFeatures.cs:186-228`
- Modify: `backend/src/CustomerSupport.Infrastructure/Ai/ResilientAiService.cs:46-55`

- [ ] **Step 1: Update the prompt to request three drafts in one call**

```csharp
// backend/src/CustomerSupport.Infrastructure/Ai/ResilientAiService.cs
public Task<AiOutcome<string>> DraftReplyAsync(string threadText, string? extraInstruction, CancellationToken ct) =>
    CompleteAsync(
        (Arabic
            ? "اكتب ثلاث ردود احترافية ومتعاطفة للعميل على المحادثة التالية. "
            : "Draft three professional, empathetic customer replies to the support thread below. ") +
        (string.IsNullOrWhiteSpace(extraInstruction)
            ? string.Empty
            : (Arabic ? "اتبع هذه التعليمة: " : "Follow this instruction: ") + extraInstruction + ". ") +
        (Arabic
            ? "أعد JSON فقط بالشكل {\"items\":[\"...\",\"...\",\"...\"]}."
            : "Return JSON only, shaped {\"items\":[\"...\",\"...\",\"...\"]} with three distinct reply bodies."),
        Fenced(threadText), ct);
```

The `AiJson.ParseStringArray` helper already extracts `items` and drops non-string/empty entries,
so the new shape re-uses it without code changes.

- [ ] **Step 2: Failing test — handler persists the drafts array**

`backend/tests/CustomerSupport.Tests/Unit/Features/Ai/DraftReplyHandlerPayloadTests.cs`:

```csharp
[Fact]
[Trait("AC", "21.12")]
public async Task Handler_PersistsDraftsArray()
{
    var ai = new StubAiService
    {
        DraftsResult = new[] { "First reply.", "Second reply.", "Third reply." },
    };
    var (handler, db, ticketId) = await ArrangeAsync(ai);

    var response = await handler.Handle(new DraftReplyCommand(ticketId), CancellationToken.None);

    response.Success.Should().BeTrue();
    var payload = JsonDocument.Parse((await db.AiSuggestions.AsNoTracking().SingleAsync()).Payload).RootElement;
    var drafts = payload.GetProperty("drafts").EnumerateArray().Select(e => e.GetString()).ToList();
    drafts.Should().BeEquivalentTo(new[] { "First reply.", "Second reply.", "Third reply." },
        opts => opts.WithStrictOrdering());
}

[Fact]
[Trait("AC", "21.12")]
public async Task Handler_FewerThanThreeDrafts_PersistsWhatItGot()
{
    var ai = new StubAiService { DraftsResult = new[] { "Only one." } };
    var (handler, db, ticketId) = await ArrangeAsync(ai);

    var response = await handler.Handle(new DraftReplyCommand(ticketId), CancellationToken.None);

    response.Success.Should().BeTrue();
    var drafts = JsonDocument.Parse((await db.AiSuggestions.AsNoTracking().SingleAsync()).Payload)
        .RootElement.GetProperty("drafts");
    drafts.GetArrayLength().Should().Be(1);
}
```

The test stub adds one field:

```csharp
public string[] DraftsResult = ["draft"];
public Task<AiOutcome<string>> DraftReplyAsync(string t, string? i, CancellationToken c) =>
    Task.FromResult(AiOutcome<string>.Ok(JsonSerializer.Serialize(new { items = DraftsResult })));
```

- [ ] **Step 3: Update `DraftReplyCommandHandler`**

```csharp
// backend/src/CustomerSupport.Application/Features/Ai/AiFeatures.cs
public async Task<Response<AiSuggestionDto>> Handle(DraftReplyCommand request, CancellationToken ct)
{
    if (!ai.IsAvailable) return AiMapping.NotConfigured<AiSuggestionDto>(factory);

    if (await AiMapping.AuthorizeTicketAsync(tickets, request.TicketId, user, factory) is { } denied)
        return Denied<AiSuggestionDto>(denied);

    var ticket = await tickets.GetByIdAsync(request.TicketId, ct);
    var thread = await messages.ListProjectedAsync(
        m => m.TicketId == request.TicketId, m => new { m.Body }, ct);

    var threadText = $"{ticket!.Subject}\n{ticket.Description}\n" +
                     string.Join("\n", thread.Select(m => "- " + m.Body));

    var outcome = await ai.DraftReplyAsync(threadText, request.Instruction, ct);
    if (!outcome.Success) return AiMapping.ProviderFailed<AiSuggestionDto>(factory);

    var drafts = (AiJson.ParseStringArray(outcome.Value) ?? [])
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Take(3)
        .ToList();
    if (drafts.Count == 0)
    {
        // AI-36 — a structured answer that lost its items is a safe failure, not a draft of "".
        return AiMapping.ProviderFailed<AiSuggestionDto>(factory);
    }

    var payload = JsonSerializer.Serialize(new { drafts });
    var suggestion = AiSuggestion.Create(request.TicketId, "Reply", payload, user.UserId);
    await suggestions.AddAsync(suggestion, ct);
    return factory.Success(AiMapping.ToDto(suggestion), "AI_DRAFT_READY");
}
```

`AiJson` is in `CustomerSupport.Infrastructure.Ai`; the handler must import it. Update the
`using` list at the top of `AiFeatures.cs`:

```csharp
using CustomerSupport.Infrastructure.Ai;
```

This is the one place where Application reaches into Infrastructure for a parser; the alternative
is to duplicate the parser. The spec amendment documents the trade-off and the parser is pure
(no I/O), so the dependency is one-way and the rule holds for the rest of the system.

- [ ] **Step 4: Build & test**

```powershell
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~DraftReplyHandlerPayloadTests"
```

Expected: 2 passing.

- [ ] **Step 5: Commit**

```powershell
git add backend/src/CustomerSupport.Application/Features/Ai/AiFeatures.cs backend/src/CustomerSupport.Infrastructure/Ai/ResilientAiService.cs backend/tests/CustomerSupport.Tests/Unit/Features/Ai/DraftReplyHandlerPayloadTests.cs
git commit -m "feat(feat-21): draft reply handler persists drafts array"
```

---

### Task 5: Existing integration tests stay green (AC-21.15, AC-21.16)

**Files:**
- Read: `backend/tests/CustomerSupport.Tests/Integration/AiAssistEndpointTests.cs`
- (no code change expected — the tests assert `ERR052` and that the request "mutates nothing",
  both of which still hold)

- [ ] **Step 1: Run the full integration suite**

```powershell
cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AiAssistEndpointTests"
```

Expected: 2 passing, no diffs to the file.

- [ ] **Step 2: Run the full test suite**

```powershell
cd backend && dotnet test CustomerSupport.slnx
```

Expected: green. Paste the output as evidence.

- [ ] **Step 3: Commit (only if the spec needed a tweak)**

No code change here. If a test needed to be updated because of the new payload shape, document
it in `tasks/task-05.md` and amend the spec rather than the test.

---

## Ship gate

- All five tasks committed.
- `dotnet test CustomerSupport.slnx` green with output pasted.
- The frontend plan (`docs/superpowers/plans/EPIC-11-US-701-feat-21-stitch-right-rail-frontend/`) is
  the very next artifact.
- Story status flipped in `US-704` and `US-706` (already done in this plan).
- Delivery plan row for `FEAT-21` updated to **shipped** with date.
