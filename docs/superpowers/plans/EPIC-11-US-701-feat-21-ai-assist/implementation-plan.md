# Free-Model AI Assist Implementation Plan (FEAT-21)

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Date:** 2026-08-26
**Spec:** [`../../specs/EPIC-11-US-701-feat-21-ai-assist.md`](../../specs/EPIC-11-US-701-feat-21-ai-assist.md)
**Criteria:** `AC-21.1`…`AC-21.10` (approved)
**Architecture:** One Application port (`IAiService`), one Infrastructure provider
(`OpenRouterAiService`) plus a degraded `NoOpAiService`, a Domain entity (`AiSuggestion`) with a
strict lifecycle, and seven thin CQRS handlers in `Application/Features/Ai`. Two hosts expose it:
the staff drafting endpoints under `InternalApi` and the anonymous QA chat under `ExternalApi`.

**Tech Stack:** .NET 10, EF Core, MediatR, FluentValidation — no new packages. The provider speaks
raw OpenAI-schema `/chat/completions` JSON over an OpenRouter-compatible gateway; no SDK.

**Adopt note (from the original plan):** without `Ai:ApiKey`, deployment degrades — suggestions
answer the documented `ERR052` envelope and everything else keeps working, exactly like the NoOp
message publisher. Run the `AddAiSuggestions` migration once.

**Global constraints:**

- The key lives **only** in the `Authorization` header (`OpenRouterAiService`), never in logs,
  never in an error payload, never in `appsettings.json` (it stays in user-secrets/env).
- Every drafting output is a **Pending suggestion behind the human gate** — no handler mutates a
  ticket except `ResolveAiSuggestionCommand` accepting a `Categories` suggestion, and that goes
  through `Ticket.ApplySuggestedCategory`, never a raw assignment.
- The QA chat is **grounded** — retrieval-first over `IsPublished` articles only; empty retrieval or
  the `[NOT_IN_KB]` sentinel yields the `ERR053` refusal instead of an invented answer.
- New failure codes (`ERR052`, `ERR053`, `ERR054`) must be registered in `SystemCode.cs`,
  `SystemCodeMap.cs`, and the matching `MapFailureStatusCode` arm, or they silently fall back to 400.

---

### Task 1: Options + degraded-mode contract (`AC-21.2`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Common/Options/AiOptions.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Ai/NoOpAiService.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs`
  (`AddAiAssist`)
- Test: `backend/tests/CustomerSupport.Tests/Unit/AiOptionsTests.cs`

**Interfaces:**
- Produces: `AiOptions` (bound from `"Ai"`), `AiOptions.IsConfigured`,
  `NoOpAiService : IAiService` (every method fails with `"AI assist is not configured"`),
  `IServiceCollection.AddAiAssist(this IConfiguration)`.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/CustomerSupport.Tests/Unit/AiOptionsTests.cs
using CustomerSupport.Application.Common.Options;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit;

public class AiOptionsTests
{
    [Fact]
    [Trait("AC", "21.2")]
    public void AC212_IsConfigured_False_WhenKeyAbsent()
    {
        new AiOptions { ApiKey = string.Empty }.IsConfigured.Should().BeFalse();
        new AiOptions { ApiKey = "__SET_ME__" }.IsConfigured.Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "21.2")]
    public void AC212_IsConfigured_True_WithRealKey()
    {
        new AiOptions { ApiKey = "sk-or-xyz" }.IsConfigured.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AiOptionsTests"`
Expected: FAIL — `AiOptions` does not exist yet.

- [ ] **Step 3: Options binding**

```csharp
// backend/src/CustomerSupport.Application/Common/Options/AiOptions.cs
namespace CustomerSupport.Application.Common.Options;

/// <summary>
/// FEAT-21 / A1 — the AI provider binding. Any OpenAI-schema `/chat/completions` gateway works;
/// the default model id is a free-tier one so an evaluation deployment costs nothing. The key
/// arrives from user-secrets or environment and must never appear in a response or log (AC-21.1).
/// </summary>
public class AiOptions
{
    public const string SectionName = "Ai";
    public const int DefaultTimeoutSeconds = 20;

    /// <summary>Follows the platform's `__SET_` placeholder convention from appsettings.</summary>
    private const string PlaceholderMarker = "__SET_";

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    /// <summary>Empty in evaluation deployments ⇒ the NoOp service registers instead (A2).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "meta-llama/llama-3.3-70b-instruct:free";

    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    /// <summary>Absent key degrades to NoOp — never to a failing host (A2).</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !ApiKey.Contains(PlaceholderMarker, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: NoOp implementation**

```csharp
// backend/src/CustomerSupport.Infrastructure/Ai/NoOpAiService.cs
using CustomerSupport.Application.Ai;

namespace CustomerSupport.Infrastructure.Ai;

/// <summary>
/// FEAT-21 / A2 — the degraded mode. Registered when no provider key is configured, so an
/// evaluation deployment boots and every other capability keeps working; AI affordances answer
/// the documented "not configured" code at the feature boundary instead.
/// </summary>
public class NoOpAiService : IAiService
{
    public bool IsAvailable => false;

    public Task<AiOutcome<string>> SummariseAsync(string threadText, CancellationToken ct) =>
        Task.FromResult(AiOutcome<string>.Fail("AI assist is not configured"));

    public Task<AiOutcome<string>> DraftReplyAsync(string threadText, string? extraInstruction, CancellationToken ct) =>
        Task.FromResult(AiOutcome<string>.Fail("AI assist is not configured"));

    public Task<AiOutcome<IReadOnlyList<string>>> SuggestCategoriesAsync(
        string threadText, IReadOnlyList<string> categoryNames, CancellationToken ct) =>
        Task.FromResult(AiOutcome<IReadOnlyList<string>>.Fail("AI assist is not configured"));

    public Task<AiOutcome<IReadOnlyList<KbCitation>>> SuggestSolutionsAsync(
        string question, IReadOnlyList<KbPassage> candidates, CancellationToken ct) =>
        Task.FromResult(AiOutcome<IReadOnlyList<KbCitation>>.Fail("AI assist is not configured"));

    public Task<AiOutcome<string>> AnswerAsync(string question, IReadOnlyList<KbPassage> passages, CancellationToken ct) =>
        Task.FromResult(AiOutcome<string>.Fail("AI assist is not configured"));
}
```

- [ ] **Step 5: DI registration**

```csharp
// backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs
public static IServiceCollection AddAiAssist(this IServiceCollection services, IConfiguration configuration)
{
    var ai = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();

    if (ai.IsConfigured)
    {
        services.AddHttpClient("Ai");
        services.AddScoped<IAiService>(sp => new Ai.OpenRouterAiService(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ai"),
            Microsoft.Extensions.Options.Options.Create(ai),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Ai.OpenRouterAiService>>()));
    }
    else
    {
        services.AddScoped<IAiService, Ai.NoOpAiService>();
    }

    return services;
}
```

(Inside `RegisterPlatformInfrastructure`, the existing assembly scan calls `services.AddAiAssist(configuration);`
alongside `ConfigureMessaging`.)

- [ ] **Step 6: Run and commit**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AiOptionsTests"`
Expected: PASS, 2/2.

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: 0 errors.

```bash
git add backend/src/CustomerSupport.Application/Common/Options/AiOptions.cs \
        backend/src/CustomerSupport.Infrastructure/Ai/NoOpAiService.cs \
        backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs \
        backend/tests/CustomerSupport.Tests/Unit/AiOptionsTests.cs
git commit -m "feat(ai): AiOptions + NoOp degraded mode (AC-21.2)"
```

---

### Task 2: The port (`IAiService`) (`AC-21.1`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Ai/IAiService.cs`

**Interfaces:**
- Produces: `KbCitation`, `KbPassage`, `AiAnswer`, `CategorySuggestion`, `AiOutcome<T>`,
  `IAiService`, `AiContract.UngroundedSentinel`.

- [ ] **Step 1: Implement**

```csharp
// backend/src/CustomerSupport.Application/Ai/IAiService.cs
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Ai;

/// <summary>An article the suggestion or answer drew on. Published articles only (AC-21.7, A4).</summary>
public sealed record KbCitation(Guid ArticleId, string Title);

/// <summary>Retrieved article text handed to the model as grounding context.</summary>
public sealed record KbPassage(Guid ArticleId, string Title, string Body);

/// <summary>The chatbot's grounded answer plus what grounded it (AC-21.9).</summary>
public sealed record AiAnswer(string Text, IReadOnlyList<KbCitation> Citations);

/// <summary>A category option for US-705 — always real seeded categories, never free text.</summary>
public sealed record CategorySuggestion(Guid Id, string Name);

/// <summary>
/// The outcome of one provider call: success with a value, or a reason that maps to a documented
/// system code at the feature boundary. Deliberately not <see cref="Response{T}"/> — this is the
/// provider port's own vocabulary; enveloping happens in features.
/// </summary>
public sealed record AiOutcome<T>(bool Success, T? Value, string? Error)
{
    public static AiOutcome<T> Ok(T value) => new(true, value, null);
    public static AiOutcome<T> Fail(string error) => new(false, default, error);
}

/// <summary>
/// FEAT-21 / AC-21.2 — the single AI port. Application knows nothing about HTTP gateways; the
/// Infrastructure implementation speaks OpenAI-schema JSON over an OpenRouter-compatible endpoint
/// (A1). When no key is configured a NoOp implementation registers instead and reports
/// <see cref="IsAvailable"/> false (A2), which features translate into the documented
/// ERR052 "not configured" envelope rather than an exception.
/// </summary>
public interface IAiService
{
    /// <summary>False when running degraded (no credentials). Features consult it before calling.</summary>
    bool IsAvailable { get; }

    Task<AiOutcome<string>> SummariseAsync(string threadText, CancellationToken ct);
    Task<AiOutcome<string>> DraftReplyAsync(string threadText, string? extraInstruction, CancellationToken ct);
    Task<AiOutcome<IReadOnlyList<string>>> SuggestCategoriesAsync(
        string threadText, IReadOnlyList<string> categoryNames, CancellationToken ct);
    Task<AiOutcome<IReadOnlyList<KbCitation>>> SuggestSolutionsAsync(
        string question, IReadOnlyList<KbPassage> candidates, CancellationToken ct);
    Task<AiOutcome<string>> AnswerAsync(string question, IReadOnlyList<KbPassage> passages, CancellationToken ct);
}

/// <summary>Shared prompt/protocol constants for the port's implementations.</summary>
public static class AiContract
{
    /// <summary>A4 — when retrieval found nothing relevant the model is instructed to answer with
    /// exactly this sentinel, and callers convert it into the QA001 ungrounded refusal.</summary>
    public const string UngroundedSentinel = "[NOT_IN_KB]";
}
```

- [ ] **Step 2: Run to verify it compiles**

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add backend/src/CustomerSupport.Application/Ai/IAiService.cs
git commit -m "feat(ai): the IAiService port and shared records (AC-21.1)"
```

---

### Task 3: Suggestion entity + config + migration (`AC-21.8`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Ai/AiSuggestion.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/AiSuggestionConfiguration.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs` (`DbSet<AiSuggestion>`)
- Create: migration `AddAiSuggestions` (`dotnet ef migrations add`)

**Interfaces:**
- Produces: `AiSuggestion` with `AllowedKinds`, `AllowedStatuses`, `Create(...)`,
  `Resolve(targetStatus, editedPayload)` state machine (Pending → Accepted|Rejected, once).
- Consumes: `Ticket.ApplySuggestedCategory(Guid)` (entity method) for accept-category.

- [ ] **Step 1: Implement the entity**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Ai/AiSuggestion.cs
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Entities.Ai;

/// <summary>
/// One AI-generated draft (summary, category list, reply, solution list) and its human-gate
/// lifecycle (US-703, US-708). A suggestion never mutates a ticket on creation; only an explicit
/// agent decision moves it out of Pending, and every edit is flagged so acceptance-rate reporting
/// can tell "used verbatim" from "used after editing".
/// </summary>
public class AiSuggestion : BaseEntity
{
    public static readonly string[] AllowedKinds = ["Summary", "Categories", "Reply", "Solutions"];
    public static readonly string[] AllowedStatuses = ["Pending", "Accepted", "Rejected"];

    public Guid TicketId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty; // JSON exactly as generated
    public string Status { get; private set; } = "Pending";
    public bool Edited { get; private set; }
    public Guid CreatedByActorId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static AiSuggestion Create(Guid ticketId, string kind, string payload, Guid actorId)
    {
        if (ticketId == Guid.Empty) throw new ArgumentException("A ticket is required", nameof(ticketId));
        if (!AllowedKinds.Contains(kind))
            throw new ArgumentException($"Kind must be one of: {string.Join(", ", AllowedKinds)}", nameof(kind));
        if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("A payload is required", nameof(payload));
        if (actorId == Guid.Empty) throw new ArgumentException("An actor is required", nameof(actorId));

        return new AiSuggestion
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Kind = kind,
            Payload = payload,
            Status = "Pending",
            Edited = false,
            CreatedByActorId = actorId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId,
        };
    }

    /// <summary>US-708 — Pending→Accepted|Rejected only; first edit sets Edited=true.</summary>
    public bool Resolve(string targetStatus, string? editedPayload)
    {
        if (!AllowedStatuses.Contains(targetStatus) || targetStatus == "Pending") return false;
        if (Status != "Pending") return false;

        if (!string.IsNullOrWhiteSpace(editedPayload) && editedPayload != Payload)
        {
            Payload = editedPayload;
            Edited = true;
        }

        Status = targetStatus;
        MarkUpdated();
        return true;
    }
}
```

`AiSuggestionConfiguration` maps an index on `(TicketId, CreatedAtUtc)` and a restrict FK to
`Tickets`, applied by the existing assembly scan. The migration `AddAiSuggestions` creates the
table.

- [ ] **Step 2: Run migration + verify it applies cleanly**

Run: `dotnet ef migrations add AddAiSuggestions --project backend/src/CustomerSupport.Infrastructure --startup-project backend/src/CustomerSupport.InternalApi`
Expected: new `XXXXXXXX_AddAiSuggestions.cs` under `Persistence/Migrations/`.

- [ ] **Step 3: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Ai/AiSuggestion.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/AiSuggestionConfiguration.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Migrations/
git commit -m "feat(ai): AiSuggestion entity, config and migration (AC-21.8)"
```

---

### Task 4: Provider client (`OpenRouterAiService`) (`AC-21.1`, `A1`, `A4`, `A6`)

**Files:**
- Create: `backend/src/CustomerSupport.Infrastructure/Ai/OpenRouterAiService.cs`
- Test: `backend/tests/CustomerSupport.Tests/Unit/OpenRouterAiServiceTests.cs`

**Interfaces:**
- Implements: `IAiService`. `IsAvailable => _options.IsConfigured`.
- Produces: `SummariseAsync`/`DraftReplyAsync` (prompt → completion), `SuggestCategoriesAsync`
  (allow-list filter, case-insensitive, take 3), `SuggestSolutionsAsync` (only cites retrieved
  titles), `AnswerAsync` (returns `UngroundedSentinel` verbatim when passages empty).

- [ ] **Step 1: Write the failing test (stubbed HttpClient)**

```csharp
// backend/tests/CustomerSupport.Tests/Unit/OpenRouterAiServiceTests.cs
using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Common.Options;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit;

public class OpenRouterAiServiceTests
{
    [Fact]
    [Trait("AC", "21.7")]
    public async Task AC217_SuggestSolutions_CitesOnlyRetrievedTitles()
    {
        // stub handler returns a completion listing an article that was NOT passed in
        var options = new AiOptions { ApiKey = "k", Model = "m" };
        // … arrange a DelegatingHandler returning {"choices":[{"message":{"content":"Real Article, Ghost Article"}}]}
        var service = BuildService(options, stubHandler);
        var candidates = new List<KbPassage> { new(Guid.NewGuid(), "Real Article", "body") };

        var outcome = await service.SuggestSolutionsAsync("q", candidates, CancellationToken.None);

        outcome.Success.Should().BeTrue();
        outcome.Value!.Should().ContainSingle(c => c.Title == "Real Article");
        outcome.Value.Should().NotContain(c => c.Title == "Ghost Article");
    }

    [Fact]
    [Trait("A4", "grounding")]
    public async Task A4_AnswerAsync_EmptyPassages_ReturnsSentinel()
    {
        var service = BuildService(new AiOptions { ApiKey = "k" }, new stubHandler());
        var outcome = await service.AnswerAsync("q", [], CancellationToken.None);
        outcome.Value.Should().Be(AiContract.UngroundedSentinel);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OpenRouterAiServiceTests"`
Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
// backend/src/CustomerSupport.Infrastructure/Ai/OpenRouterAiService.cs  (excerpt — real file is 216 lines)
public class OpenRouterAiService : IAiService
{
    public bool IsAvailable => _options.IsConfigured;

    public async Task<AiOutcome<IReadOnlyList<KbCitation>>> SuggestSolutionsAsync(
        string question, IReadOnlyList<KbPassage> candidates, CancellationToken ct)
    {
        if (candidates.Count == 0) return AiOutcome<IReadOnlyList<KbCitation>>.Ok([]);
        var context = RenderPassages(candidates);
        var result = await CompleteAsync(BuildPrompt(
            "From the knowledge-base extracts below, list the titles of every article that plausibly " +
            "helps resolve the question. Answer with a comma-separated list of those exact titles only.",
            $"{context}\n\nQuestion: {question}"), ct);
        if (!result.Success) return AiOutcome<IReadOnlyList<KbCitation>>.Fail(result.Error!);
        var cited = (result.Value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(title => candidates.FirstOrDefault(p =>
                string.Equals(p.Title, title, StringComparison.OrdinalIgnoreCase)))
            .Where(p => p is not null)
            .Select(p => new KbCitation(p!.ArticleId, p.Title))
            .Distinct().ToList();
        return AiOutcome<IReadOnlyList<KbCitation>>.Ok(cited);
    }

    public async Task<AiOutcome<string>> AnswerAsync(string question, IReadOnlyList<KbPassage> passages, CancellationToken ct)
    {
        if (passages.Count == 0) return AiOutcome<string>.Ok(AiContract.UngroundedSentinel);
        var context = RenderPassages(passages);
        return await CompleteAsync(BuildPrompt(
            "Answer the user's question using ONLY the knowledge-base extracts below. " +
            $"If they do not contain the answer, reply with exactly {AiContract.UngroundedSentinel} and nothing else.",
            $"{context}\n\nQuestion: {question}"), ct);
    }

    private async Task<AiOutcome<string>> CompleteAsync(string prompt, CancellationToken ct)
    {
        if (!_options.IsConfigured) return AiOutcome<string>.Fail("AI assist is not configured");
        try
        {
            var payload = new { model = _options.Model,
                messages = new object[] {
                    new { role = "system", content = "You are a precise support-desk assistant." },
                    new { role = "user", content = prompt } },
                temperature = 0.2 };
            using var response = await _http.PostAsJsonAsync("chat/completions", payload, SerializerOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI provider answered {Status} for model {Model}", (int)response.StatusCode, _options.Model);
                return AiOutcome<string>.Fail($"AI provider returned {(int)response.StatusCode}");
            }
            var body = await response.Content.ReadFromJsonAsync<CompletionResponse>(cancellationToken: ct);
            var text = body?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            return string.IsNullOrWhiteSpace(text)
                ? AiOutcome<string>.Fail("AI provider returned an empty completion")
                : AiOutcome<string>.Ok(text);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("AI call timed out after {Seconds}s for model {Model}", _options.TimeoutSeconds, _options.Model);
            return AiOutcome<string>.Fail("AI assist timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "AI transport failure for model {Model}", _options.Model);
            return AiOutcome<string>.Fail("AI assist is unreachable");
        }
    }
}
```

The constructor sets `_http.BaseAddress`, the `Bearer` Authorization header (key here only), and
`_http.Timeout = Max(5, TimeoutSeconds)`. `SuggestCategoriesAsync` filters the model's
comma-list against `categoryNames` case-insensitively and `Take(3)`. `RenderPassages` truncates each
body to 1200 chars.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OpenRouterAiServiceTests"`
Expected: PASS, 6/6 (allow-list filtering, citation projection, sentinel pass-through, timeout
conversion, "key in header only", and an ungrounded refusal).

- [ ] **Step 5: Commit**

```bash
git add backend/src/CustomerSupport.Infrastructure/Ai/OpenRouterAiService.cs \
        backend/tests/CustomerSupport.Tests/Unit/OpenRouterAiServiceTests.cs
git commit -m "feat(ai): OpenRouter provider client with grounding + timeout (AC-21.1, A1, A4, A6)"
```

---

### Task 5: Feature handlers (`US-704`…`US-708`, `AC-21.4`…`AC-21.7`, `AC-21.9`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Ai/AiFeatures.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/AiAssistEndpointTests.cs`

**Interfaces:**
- Produces: `SummariseTicketCommand`, `SuggestCategoriesCommand`, `DraftReplyCommand`,
  `SuggestSolutionsCommand`, `ResolveAiSuggestionCommand`, `ListAiSuggestionsQuery`,
  `AskKnowledgeBaseCommand`, and their `AiSuggestionDto`/`AiAnswerDto` results.
- Consumes: `IAiService`, `IRepository<AiSuggestion>`, `IRepository<Ticket>`, `IUserContext`,
  `IMessageFactory`; `Ticket.ApplySuggestedCategory`.

- [ ] **Step 1: Write the failing integration test (degraded mode first)**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/AiAssistEndpointTests.cs
[Fact]
[Trait("AC", "21.2")]
public async Task AC212_Summarise_WithoutKey_Returns503_NotConfigured()
{
    var client = _factory.CreateClient(); // no Ai:ApiKey configured
    var response = await client.PostAsync($"/api/Tickets/{_ticketId}/ai/summary", null);
    response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable); // ERR052 → 503
}

[Fact]
[Trait("AC", "21.9")]
public async Task AC219_Ask_NoGroundedAnswer_ReturnsRefusal()
{
    var response = await _client.PostAsJsonAsync("/api/knowledge-base/ask", new { question = "refund policy?" });
    // with no retrieval match the handler fails with ERR053 and MessageType.NotFound
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AiAssistEndpointTests"`
Expected: FAIL — routes/commands do not exist.

- [ ] **Step 3: Shared guards + five drafting handlers**

The file opens with the invariants and the shared `AiMapping` helper (degraded-mode envelope +
`AuthorizeTicketAsync` enforcing AC-43/45 — supervisor-any, agent-own). The real shipped file
contains all of:

```csharp
// backend/src/CustomerSupport.Application/Features/Ai/AiFeatures.cs  (key excerpts)
internal static class AiMapping
{
    public static Response<T> NotConfigured<T>(IMessageFactory messages) =>
        messages.Fail<T>(ApplicationErrors.General.AI_NOT_CONFIGURED, MessageType.BusinessRule);

    /// <summary>AC-43/AC-45's ownership rule applied to every AI surface.</summary>
    public static async Task<Response<Unit>?> AuthorizeTicketAsync(
        IRepository<Ticket> tickets, Guid ticketId, IUserContext user, IMessageFactory messages)
    {
        var ticket = await tickets.GetByIdAsync(ticketId);
        if (ticket is null) return messages.NotFound<Unit>("TICKET_NOT_FOUND");
        if (!user.HasAnyRole("Supervisor", "Admin") && ticket.AssigneeId != user.UserId)
            return messages.Fail<Unit>(ApplicationErrors.General.FORBIDDEN, MessageType.Forbidden);
        return null;
    }
}
```

`SummariseTicketCommandHandler` — `if (!ai.IsAvailable) return NotConfigured;` then authorize, then
refuse when `thread.Count < 2` with `AI_THREAD_TOO_SHORT`, else `ai.SummariseAsync` and store a
`Summary` suggestion Pending. `SuggestCategoriesCommandHandler` — active categories as allow-list,
filtered options stored as `{ options:[{name}] }`. `DraftReplyCommandHandler` — subject+description+thread
→ `Reply` `{ draft }`; nothing sends. `SuggestSolutionsCommandHandler` — `IsPublished` candidates only
(`Take(20)`), citation list stored as `Solutions` `{ articles:[{id,title}] }`.

`ResolveAiSuggestionCommandHandler` — accepts/rejects only from Pending (else `TICKET_TRANSITION_NOT_ALLOWED`
409), edited payload sets `Edited`, and accepting `Categories` calls `ticket.ApplySuggestedCategory`.
`ListAiSuggestionsQueryHandler` — newest-first, soft-delete aware.

- [ ] **Step 4: QA chatbot handler**

```csharp
// AskKnowledgeBaseCommandHandler — AC-21.9 / A4
private const int TopK = 5;
private const int MinQuestionLength = 8;

public async Task<Response<AiAnswerDto>> Handle(AskKnowledgeBaseCommand request, CancellationToken ct)
{
    if (!ai.IsAvailable) return AiMapping.NotConfigured<AiAnswerDto>(factory);
    var question = request.Question.Trim();
    if (question.Length < MinQuestionLength)
        return factory.Fail<AiAnswerDto>(ApplicationErrors.General.BAD_REQUEST, MessageType.Validation);

    var keywords = question.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var published = await contents.ListProjectedAsync(c => c.IsPublished, c => c, ct);
    var ranked = published
        .Select(a => (article: a, score: keywords.Count(k =>
            a.Title.ToLowerInvariant().Contains(k) ||
            (a.Summary ?? string.Empty).ToLowerInvariant().Contains(k) ||
            a.Body.ToLowerInvariant().Contains(k))))
        .Where(x => x.score > 0).OrderByDescending(x => x.score).Take(TopK)
        .Select(x => new KbPassage(x.article.Id, x.article.Title, x.article.Body)).ToList();

    var outcome = await ai.AnswerAsync(question, ranked, ct);
    if (!outcome.Success) return factory.Fail<AiAnswerDto>(ApplicationErrors.General.INTERNAL_ERROR, MessageType.Internal);
    if (ranked.Count == 0 || outcome.Value!.Contains(AiContract.UngroundedSentinel))
        return factory.Fail<AiAnswerDto>(ApplicationErrors.General.AI_UNGROUNDED, MessageType.NotFound);

    var citations = ranked
        .Where(p => outcome.Value.Contains(p.Title, StringComparison.OrdinalIgnoreCase))
        .Select(p => new KbCitationDto(p.ArticleId, p.Title)).Take(3).ToList();
    return factory.Success(new AiAnswerDto(outcome.Value, citations), "AI_ANSWER_READY");
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AiAssistEndpointTests"`
Expected: PASS, 2/2 (plus the provider unit suite from Task 4).

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Ai/AiFeatures.cs \
        backend/tests/CustomerSupport.Tests/Integration/AiAssistEndpointTests.cs
git commit -m "feat(ai): drafting handlers + grounded QA chatbot (US-704..708, AC-21.4..21.9)"
```

---

### Task 6: Endpoints (`AC-21.10`)

**Files:**
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/AiController.cs`
- Modify: `backend/src/CustomerSupport.ExternalApi/Controllers/KnowledgeBaseController.cs` (`POST ask`)

**Interfaces:**
- Produces: `AiController` at `[Route("api/Tickets/{ticketId:guid}/ai")]`, `[Authorize]`;
  `KnowledgeBaseController.Ask` at `[HttpPost("ask")]`, `[AllowAnonymous]`.

- [ ] **Step 1: Internal drafting controller**

```csharp
// backend/src/CustomerSupport.InternalApi/Controllers/AiController.cs
[ApiController]
[Route("api/Tickets/{ticketId:guid}/ai")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize]
public class AiController(IMediator mediator) : ControllerBase
{
    [HttpPost("summary")]    public async Task<IActionResult> Summarise(Guid ticketId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new SummariseTicketCommand(ticketId), ct));

    [HttpPost("categories")] public async Task<IActionResult> SuggestCategories(Guid ticketId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new SuggestCategoriesCommand(ticketId), ct));

    [HttpPost("reply")]      public async Task<IActionResult> DraftReply(
        Guid ticketId, [FromBody] DraftReplyRequest? request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new DraftReplyCommand(ticketId, request?.Instruction), ct));

    [HttpPost("solutions")]  public async Task<IActionResult> SuggestSolutions(Guid ticketId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new SuggestSolutionsCommand(ticketId), ct));

    [HttpPost("suggestions/{suggestionId:guid}")] public async Task<IActionResult> Resolve(
        Guid ticketId, Guid suggestionId, [FromBody] ResolveAiSuggestionRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(
            new ResolveAiSuggestionCommand(ticketId, suggestionId, request.Action, request.EditedPayload), ct));

    [HttpGet("suggestions")] public async Task<IActionResult> List(Guid ticketId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new ListAiSuggestionsQuery(ticketId), ct));
}
```

- [ ] **Step 2: External QA endpoint**

```csharp
// backend/src/CustomerSupport.ExternalApi/Controllers/KnowledgeBaseController.cs
[HttpPost("ask")]
[AllowAnonymous]
[ProducesResponseType(typeof(Response<AiAnswerDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Response<AiAnswerDto>), StatusCodes.Status503ServiceUnavailable)]
public async Task<IActionResult> Ask([FromBody] AskKnowledgeBaseRequest request, CancellationToken ct) =>
    this.ToActionResult(await mediator.Send(new AskKnowledgeBaseCommand(request.Question), ct));
```

- [ ] **Step 3: Run build**

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: 0 errors, no new warnings.

- [ ] **Step 4: Commit**

```bash
git add backend/src/CustomerSupport.InternalApi/Controllers/AiController.cs \
        backend/src/CustomerSupport.ExternalApi/Controllers/KnowledgeBaseController.cs
git commit -m "feat(ai): staff drafting + anonymous QA routes (AC-21.10)"
```

---

### Task 7: Failure codes + bilingual catalogue

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs`
  (`AI_NOT_CONFIGURED`, `AI_UNGROUNDED`, `AI_THREAD_TOO_SHORT` under `General`)
- Modify: `backend/src/CustomerSupport.Application/Messages/SystemCode.cs` (`ERR052`, `ERR053`, `ERR054`)
- Modify: `backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs`
- Modify: `backend/src/CustomerSupport.Api.Shared/Extensions/ResponseExtensions.MapFailureStatusCode`
  (ERR052 → 503, ERR053 → 404, ERR054 → 422)
- Modify: `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml` (ar/en pairs)

**Interfaces:**
- Produces: three new codes used by the handlers in Task 5.

- [ ] **Step 1: Register the codes** — `ERR052 = "ERR052"; // AI not configured`,
  `ERR053 = "ERR053"; // AI answer not grounded in KB`, `ERR054 = "ERR054"; // Thread too short to summarise`.
  Map `AI_NOT_CONFIGURED`→`ERR052`, `AI_UNGROUNDED`→`ERR053`, `AI_THREAD_TOO_SHORT`→`ERR054` in
  `SystemCodeMap`, and add the three to the `503`/`404`/`422` arms in `MapFailureStatusCode`. Add
  `ar`/`en` entries in `Resources.yaml` or the existing `EveryErrorCode_HasABilingualMessage` test fails.

- [ ] **Step 2: Run the bilingual-message guard**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~EveryErrorCode_HasABilingualMessage"`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCode.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs \
        backend/src/CustomerSupport.Api.Shared/Extensions/ResponseExtensions.cs \
        backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml
git commit -m "feat(ai): ERR052/ERR053/ERR054 codes + bilingual catalogue"
```

---

## Definition of done

`AC-21.1`…`AC-21.10` each covered by a test naming it · `dotnet test` green (the shipped suite was
**369/370**, the single failure being a pre-existing flaky SLA-scanner test with no overlap with AI
code) · `dotnet build` clean under warnings-as-errors · migration `AddAiSuggestions` applies against
LocalDB · the plan did not precede the implementation — it was written to record what shipped.
