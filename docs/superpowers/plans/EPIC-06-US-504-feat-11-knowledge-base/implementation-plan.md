# Knowledge Base (FEAT-11, backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish/archive commands, version history, real category/tag taxonomy, FAQ curation,
article-ticket linking, Arabic-aware search, and view/vote tracking — all extending the inherited
`Content` aggregate, none of it a parallel schema (`AC-165`–`AC-195`).

**Architecture:** New CQRS features under `Features/Contents/` and a new `Features/ContentCategories/`,
following this codebase's existing `Content*CommandHandler`/`*QueryHandler` shape exactly (see
`CreateContentCommandHandler`, `UpdateContentCommandHandler`, `GetContentsQueryHandler` — read before
writing new code that should look identical in style). Five new entities
(`ContentCategory`, `ContentVersion`, `ContentView`, `ContentVote`, `ContentTicketLink`), one
migration, extensions to the existing `Content` entity and `ContentDto`.

**Tech Stack:** .NET 10, EF Core, MediatR, FluentValidation — no new packages.

**Spec:** [`docs/superpowers/specs/EPIC-06-US-504-knowledge-base.md`](../../specs/EPIC-06-US-504-knowledge-base.md)

**Not implemented this pass.** This plan is written and committed ahead of any code that implements
it, per explicit instruction — execution is a future session's work.

## Global Constraints

- Every new domain entity that is an audit/history trail (`ContentVersion`, `ContentView`,
  `ContentTicketLink`) implements `IAppendOnlyEntity`, matching `TicketHistory`/`SLAEvent`
  (`AppDbContext`'s `SaveChanges` guard enforces this — see `IAppendOnlyEntity`,
  `docs/adr/0010-*.md`). `ContentVote` is **not** append-only — `AC-188` requires updating a row
  in place.
- Every new failure code is registered in **all three** places or it silently falls back to `400`
  — `SystemCode.cs`, `SystemCodeMap.cs`, and (for 404/409 codes) the matching switch arm in
  `ResponseExtensions.MapFailureStatusCode`. This is the single most-repeated lesson from this
  project's own task records (`FEAT-16`, `FEAT-19`) — read `docs/superpowers/plans/EPIC-12-US-000-feat-16-organisation-structure/README.md`'s "Deviation found and fixed" section before writing Task 1.
- Every new unique index (`ContentCategory(Name, ParentId)`, `ContentTicketLink(TicketId, ContentId)`,
  `ContentVote(ContentId, UserId)`) is paired with `IDbExceptionTranslator` handling in its
  Create/Vote handler — the same FEAT-16 lesson, second half: an unpaired unique index 500s on
  violation instead of 409ing.
- `Content.Category` (the free-text field) is **not deleted** in this migration — it becomes
  write-dead (nothing sets it going forward) but stays readable during rollout, per spec A2. A
  follow-up migration removing it is explicitly out of this plan's scope.
- Every command handler that mutates `Content` calls the new `Content.RecordChange(summary)`
  (Task 2) in the same `SaveChangesAsync` as its own change — versioning is infrastructure every
  later task's handler must also wire in, not a bolt-on retrofit.

---

### Task 1: Publish/Archive commands (`AC-165`, `AC-166`, `AC-167`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Contents/Commands/PublishContent/PublishContentCommand.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Contents/Commands/PublishContent/PublishContentCommandHandler.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Contents/Commands/ArchiveContent/ArchiveContentCommand.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Contents/Commands/ArchiveContent/ArchiveContentCommandHandler.cs`
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/ContentsController.cs`
- Modify: `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs`, `SystemCode.cs`,
  `SystemCodeMap.cs`, `backend/src/CustomerSupport.Api.Shared/Extensions/ResponseExtensions.cs`,
  `backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml`
- Test: `backend/tests/CustomerSupport.Tests/Integration/ContentPublishArchiveEndpointTests.cs`

**Interfaces:**
- Consumes: `Content.Publish()`/`Content.Archive()` — **already exist**
  (`CustomerSupport.Domain/Entities/Content/Content.cs`), already throw `InvalidOperationException`
  on an illegal transition via `ContentStatus.CanTransitionTo`. This task adds the CQRS/API layer
  around them; it does not touch the domain method bodies.
- Produces: `PublishContentCommand(Guid Id) : ICommand<Response<Guid>>`,
  `ArchiveContentCommand(Guid Id) : ICommand<Response<Guid>>`.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/ContentPublishArchiveEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>FEAT-11, AC-165..AC-167 — dedicated publish/archive commands over the existing
/// `Content.Publish()`/`Archive()` domain methods.</summary>
public class ContentPublishArchiveEndpointTests : IAsyncLifetime
{
    private readonly CrmApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        (_client, _) = await _factory.CreateAuthenticatedClientAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<Guid> CreateDraftAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/Contents", new
        {
            title = $"Publish fixture {Guid.NewGuid():N}",
            body = "Body text.",
            contentType = "Article",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Response<Guid>>())!.Data;
    }

    [Fact]
    [Trait("AC", "165")]
    public async Task AC165_Publish_FromDraft_SetsStatusPublished()
    {
        var id = await CreateDraftAsync();

        var response = await _client.PostAsync($"/api/Contents/{id}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await _client.GetFromJsonAsync<Response<ContentRow>>($"/api/Contents/{id}");
        detail!.Data!.Status.Should().Be("Published");
        detail.Data.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("AC", "167")]
    public async Task AC167_Publish_FromArchived_Returns409()
    {
        var id = await CreateDraftAsync();
        (await _client.PostAsync($"/api/Contents/{id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.PostAsync($"/api/Contents/{id}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("AC", "166")]
    public async Task AC166_Archive_FromDraft_SetsStatusArchived()
    {
        var id = await CreateDraftAsync();

        var response = await _client.PostAsync($"/api/Contents/{id}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await _client.GetFromJsonAsync<Response<ContentRow>>($"/api/Contents/{id}");
        detail!.Data!.Status.Should().Be("Archived");
    }

    public sealed record ContentRow(string Status, DateTime? PublishedAt);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentPublishArchiveEndpointTests"`
Expected: FAIL — 404, routes don't exist yet.

- [ ] **Step 3: Add the two commands**

```csharp
// backend/src/CustomerSupport.Application/Features/Contents/Commands/PublishContent/PublishContentCommand.cs
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Contents.Commands.PublishContent;

public record PublishContentCommand(Guid Id) : ICommand<Response<Guid>>;
```

```csharp
// backend/src/CustomerSupport.Application/Features/Contents/Commands/PublishContent/PublishContentCommandHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Contents.Commands.PublishContent;

public class PublishContentCommandHandler(
    IRepository<Content> contentRepository,
    IUnitOfWork unitOfWork,
    IMessageFactory messages,
    ILogger<PublishContentCommandHandler> logger)
    : ICommandHandler<PublishContentCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(PublishContentCommand request, CancellationToken ct)
    {
        var content = await contentRepository.GetByIdAsync(request.Id, ct);
        if (content == null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Content.NOT_FOUND);
        }

        try
        {
            content.Publish();
        }
        catch (InvalidOperationException)
        {
            logger.LogWarning("Publish refused — content {ContentId} in status {Status}", content.Id, content.Status);
            return messages.Fail<Guid>(ApplicationErrors.Content.NOT_PUBLISHABLE, MessageType.Conflict);
        }

        content.RecordChange("Published");
        contentRepository.Update(content);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(content.Id, ApplicationErrors.Content.PUBLISHED);
    }
}
```

`ArchiveContentCommand`/`Handler` are the mirror image — `content.Archive()`, catch
`InvalidOperationException` → `ApplicationErrors.Content.NOT_ARCHIVABLE` (409), success code
`ApplicationErrors.Content.ARCHIVED` (already exists in `ApplicationErrors.Content`, unused until
now — same for `PUBLISHED`).

- [ ] **Step 4: Register the new error codes**

`ApplicationErrors.cs`, inside `public static class Content`, add:

```csharp
public const string NOT_PUBLISHABLE = "CONTENT_NOT_PUBLISHABLE";
public const string NOT_ARCHIVABLE = "CONTENT_NOT_ARCHIVABLE";
```

`SystemCode.cs`: add `ERR055 = "ERR055"; // Content not publishable from current status` and
`ERR056 = "ERR056"; // Content not archivable from current status`. `SystemCodeMap.cs`: map both
domain keys to the new codes. `ResponseExtensions.MapFailureStatusCode`: add both to the `409`
switch arm, alongside `NAME_EXISTS`/similar existing conflict codes. `Resources.yaml`: add ar/en
pairs for both keys (required by the existing `EveryErrorCode_HasABilingualMessage` test).

- [ ] **Step 5: Add the controller actions**

In `ContentsController.cs`, add after `Delete`:

```csharp
    /// <summary>Publishes a Draft article — AC-165, AC-167.</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new Commands.PublishContent.PublishContentCommand(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Archives an article — AC-166.</summary>
    [HttpPost("{id:guid}/archive")]
    [Authorize]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new Commands.ArchiveContent.ArchiveContentCommand(id), ct);
        return this.ToActionResult(result);
    }
```

(add `using CustomerSupport.Application.Features.Contents.Commands.PublishContent;` and
`...ArchiveContent;` to the top of the file rather than fully-qualifying inline, matching the
file's existing `using` style — shown fully-qualified above only so the diff is unambiguous.)

- [ ] **Step 6: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentPublishArchiveEndpointTests"`
Expected: PASS, 3/3.

- [ ] **Step 7: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Contents/Commands/PublishContent/ \
        backend/src/CustomerSupport.Application/Features/Contents/Commands/ArchiveContent/ \
        backend/src/CustomerSupport.InternalApi/Controllers/ContentsController.cs \
        backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCode.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs \
        backend/src/CustomerSupport.Api.Shared/Extensions/ResponseExtensions.cs \
        backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml \
        backend/tests/CustomerSupport.Tests/Integration/ContentPublishArchiveEndpointTests.cs
git commit -m "feat(kb): dedicated publish/archive commands (AC-165, AC-166, AC-167)"
```

---

### Task 2: Article versioning (`AC-168`, `AC-169`, `AC-170`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Content/ContentVersion.cs`
- Modify: `backend/src/CustomerSupport.Domain/Entities/Content/Content.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ContentVersionConfiguration.cs`
- Modify: `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs` (add `DbSet<ContentVersion>`)
- Modify: `CreateContentCommandHandler.cs`, `UpdateContentCommandHandler.cs`,
  `PublishContentCommandHandler.cs`, `ArchiveContentCommandHandler.cs` (call `RecordChange`)
- Create: `backend/src/CustomerSupport.Application/Features/Contents/Queries/GetContentVersions/`
  (Query + Handler)
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/ContentsController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/ContentVersioningEndpointTests.cs`

**Interfaces:**
- Produces: `Content.Version` (int), `Content.RecordChange(string changeSummary)` — appends a
  `ContentVersion` row via a domain event pattern (see below) and increments `Version`.
  `ContentVersion(Guid Id, Guid ContentId, int VersionNumber, Guid AuthorId, string ChangeSummary,
  string TitleSnapshot, string BodySnapshot, DateTime CreatedAt)`.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/CustomerSupport.Tests/Integration/ContentVersioningEndpointTests.cs
[Fact]
[Trait("AC", "168")]
public async Task AC168_SavingChange_IncrementsVersion()
{
    var id = await CreateDraftAsync(); // version 1 per AC-169

    await _client.PutAsJsonAsync($"/api/Contents/{id}", new { title = "Updated title" });

    var detail = await _client.GetFromJsonAsync<Response<ContentRow>>($"/api/Contents/{id}");
    detail!.Data!.Version.Should().Be(2);
}

[Fact]
[Trait("AC", "170")]
public async Task AC170_VersionHistory_ReturnsNewestFirstWithMetadata()
{
    var id = await CreateDraftAsync();
    await _client.PutAsJsonAsync($"/api/Contents/{id}", new { title = "First edit" });
    await _client.PutAsJsonAsync($"/api/Contents/{id}", new { title = "Second edit" });

    var versions = await _client.GetFromJsonAsync<Response<List<VersionRow>>>($"/api/Contents/{id}/versions");

    versions!.Data.Should().HaveCount(3); // 1 (create) + 2 edits
    versions.Data![0].VersionNumber.Should().Be(3);
    versions.Data[0].ChangeSummary.Should().NotBeNullOrEmpty();
}

public sealed record VersionRow(int VersionNumber, Guid AuthorId, string ChangeSummary, DateTime CreatedAt);
```

(`ContentRow` extended with `int Version` alongside `Status`/`PublishedAt` from Task 1's test file.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentVersioningEndpointTests"`
Expected: FAIL — `Version`/`/versions` don't exist.

- [ ] **Step 3: `ContentVersion` entity**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Content/ContentVersion.cs
using CustomerSupport.Domain.Entities;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Domain.Entities.Content;

/// <summary>One saved snapshot of an article — AC-168/169/170. Append-only: a version record is
/// never edited or removed after it's written, matching TicketHistory/SLAEvent's guard.</summary>
public class ContentVersion : BaseEntity, IAppendOnlyEntity
{
    public Guid ContentId { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid AuthorId { get; private set; }
    public string ChangeSummary { get; private set; } = string.Empty;
    public string TitleSnapshot { get; private set; } = string.Empty;
    public string BodySnapshot { get; private set; } = string.Empty;

    public static ContentVersion Create(Guid contentId, int versionNumber, Guid authorId,
        string changeSummary, string titleSnapshot, string bodySnapshot) => new()
    {
        Id = Guid.NewGuid(),
        ContentId = contentId,
        VersionNumber = versionNumber,
        AuthorId = authorId,
        ChangeSummary = changeSummary,
        TitleSnapshot = titleSnapshot,
        BodySnapshot = bodySnapshot,
        CreatedAt = DateTime.UtcNow,
    };
}
```

- [ ] **Step 4: `Content` gains `Version` and `RecordChange`**

In `Content.cs`, add the field and a collection the handler reads to persist the version row (the
entity itself does not depend on `IRepository<ContentVersion>` — it stages the snapshot via a
public method the handler calls once, keeping the aggregate free of infrastructure concerns):

```csharp
    public int Version { get; private set; } = 1;

    /// <summary>Snapshots the article's current title/body under a new version number, for the
    /// handler to persist as a ContentVersion row in the same SaveChangesAsync. Called by every
    /// mutating command (AC-168) — including Publish/Archive, since a status transition is itself
    /// a change worth recording (spec AC-165/166's "recorded in the version history").</summary>
    public ContentVersionSnapshot RecordChange(string changeSummary)
    {
        Version++;
        MarkUpdated();
        return new ContentVersionSnapshot(Version, Title, Body);
    }
```

```csharp
// Add to Content.cs or a small adjacent file — a plain DTO, not an entity.
public readonly record struct ContentVersionSnapshot(int VersionNumber, string Title, string Body);
```

`Content.Create(...)` also stages an initial version-1 snapshot — the handler in Step 5 creates
`ContentVersion` with `VersionNumber = 1` directly from the freshly created entity rather than
calling `RecordChange` (which would bump to 2), satisfying `AC-169`.

- [ ] **Step 5: Wire every mutating handler**

`CreateContentCommandHandler`, after `AddAsync`:
```csharp
        var initialVersion = Domain.Entities.Content.ContentVersion.Create(
            content.Id, 1, request.AuthorId, "Created", content.Title, content.Body);
        await contentVersionRepository.AddAsync(initialVersion, ct);
```
(constructor gains `IRepository<ContentVersion> contentVersionRepository`.)

`UpdateContentCommandHandler`/`PublishContentCommandHandler`/`ArchiveContentCommandHandler`, right
before `SaveChangesAsync`:
```csharp
        var snapshot = content.RecordChange("Updated"); // or "Published" / "Archived"
        var version = Domain.Entities.Content.ContentVersion.Create(
            content.Id, snapshot.VersionNumber, /* current user id */, "Updated", snapshot.Title, snapshot.Body);
        await contentVersionRepository.AddAsync(version, ct);
```
`UpdateContentCommandHandler` needs `IUserContext userContext` added to its constructor (not
currently injected) to know the author id — matching how `RecordTicketMessageCommandHandler`
already sources the actor from `IUserContext`, never from the request body.

`ChangeSummary` for `UpdateContentCommandHandler` should name what actually changed (e.g.
`"Updated: Title, Body"`) rather than the fixed string `"Updated"` shown above — build it from
which of `request.Title`/`Body`/`Summary`/`Category`/`Tags` are non-null, satisfying `AC-170`'s
"change summary" expectation precisely rather than a placeholder.

- [ ] **Step 6: Query + endpoint**

```csharp
// backend/src/CustomerSupport.Application/Features/Contents/Queries/GetContentVersions/GetContentVersionsQuery.cs
public record GetContentVersionsQuery(Guid ContentId) : IQuery<Response<IReadOnlyList<ContentVersionDto>>>;
```
Handler: `contentVersionRepository.ListOrderedAsync(v => v.ContentId == request.ContentId, v => v.VersionNumber, descending: true, ct)`.
Controller: `GET /api/Contents/{id}/versions`, `[Authorize]`.

- [ ] **Step 7: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentVersioningEndpointTests|FullyQualifiedName~ContentPublishArchiveEndpointTests"`
Expected: PASS, both files.

- [ ] **Step 8: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Content/ContentVersion.cs \
        backend/src/CustomerSupport.Domain/Entities/Content/Content.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ContentVersionConfiguration.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs \
        backend/src/CustomerSupport.Application/Features/Contents/Commands/ \
        backend/src/CustomerSupport.Application/Features/Contents/Queries/GetContentVersions/ \
        backend/src/CustomerSupport.InternalApi/Controllers/ContentsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ContentVersioningEndpointTests.cs
git commit -m "feat(kb): article version history (AC-168, AC-169, AC-170)"
```

---

### Task 3: Category taxonomy (`AC-171`, `AC-172`, `AC-173`, `AC-174`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Content/ContentCategory.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ContentCategoryConfiguration.cs`
- Modify: `Content.cs` (add `CategoryId`), `ContentDto.cs` (add `CategoryId`, `CategoryName`)
- Create: `Features/ContentCategories/Commands/CreateContentCategory/`,
  `Features/ContentCategories/Commands/AssignCategory/`,
  `Features/ContentCategories/Queries/GetContentCategoryTree/`
- Create: `backend/src/CustomerSupport.InternalApi/Controllers/ContentCategoriesController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/ContentCategoryEndpointTests.cs`

**Interfaces:**
- Produces: `ContentCategory(Guid Id, string Name, string Slug, Guid? ParentId, int SortOrder, bool
  IsActive)`, matching `Department`/`Branch`'s shape (`FEAT-16`) exactly — read
  `Domain/Entities/Organisation/Department.cs` before writing this file so the two look like they
  came from the same hand.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "171")]
public async Task AC171_CreateCategory_WithParent_IsRetrievable()
{
    var parentId = await CreateCategoryAsync("Billing", null);

    var response = await _client.PostAsJsonAsync("/api/ContentCategories", new { name = "Refunds", parentId });

    response.StatusCode.Should().Be(HttpStatusCode.Created);
}

[Fact]
[Trait("AC", "171")]
public async Task AC171_CreateCategory_DuplicateNameUnderSameParent_Returns409()
{
    await CreateCategoryAsync("Billing", null);

    var response = await _client.PostAsJsonAsync("/api/ContentCategories", new { name = "Billing", parentId = (Guid?)null });

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
[Trait("AC", "174")]
public async Task AC174_ListCategories_ReturnsNestedTree()
{
    var parentId = await CreateCategoryAsync("Billing", null);
    await CreateCategoryAsync("Refunds", parentId);

    var tree = await _client.GetFromJsonAsync<Response<List<CategoryNode>>>("/api/ContentCategories");

    var billing = tree!.Data!.Single(c => c.Id == parentId);
    billing.Children.Should().ContainSingle(c => c.Name == "Refunds");
}

[Fact]
[Trait("AC", "172")]
public async Task AC172_AssignCategory_UnknownId_Returns404()
{
    var contentId = await CreateDraftAsync();

    var response = await _client.PutAsJsonAsync($"/api/Contents/{contentId}/category", new { categoryId = Guid.NewGuid() });

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

public sealed record CategoryNode(Guid Id, string Name, Guid? ParentId, List<CategoryNode> Children);
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentCategoryEndpointTests"`
Expected: FAIL — 404, nothing exists.

- [ ] **Step 3: Entity + config**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Content/ContentCategory.cs
using CustomerSupport.Domain.Entities;

namespace CustomerSupport.Domain.Entities.Content;

public class ContentCategory : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static ContentCategory Create(string name, Guid? parentId, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        return new ContentCategory
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = Slugify(name),
            ParentId = parentId,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            MarkUpdated();
        }
    }

    private static string Slugify(string name) =>
        name.Trim().ToLowerInvariant().Replace(' ', '-');
}
```

EF config: unique index on `(Name, ParentId)` (backing `AC171_CreateCategory_DuplicateNameUnderSameParent_Returns409`), self-referencing FK on `ParentId` with `DeleteBehavior.Restrict` (a parent with children must not cascade-delete them).

- [ ] **Step 4: Create + tree query, `IDbExceptionTranslator` pairing**

`CreateContentCategoryCommandHandler` catches the unique-violation via `IDbExceptionTranslator`
exactly like `CreateDepartmentCommandHandler` does (`FEAT-16`) — read that handler first, mirror
its `try/catch IsUniqueViolation` shape precisely rather than re-deriving it.

`GetContentCategoryTreeQueryHandler`: `categoryRepository.ListAsync(c => c.IsActive, ct)`, then
build the tree in memory (a `Dictionary<Guid?, List<ContentCategory>>` grouped by `ParentId`,
recursively projected into `CategoryNode`) — the category count here is small (dozens, not
thousands), so an in-memory tree build is the right trade, matching this project's established
"defensible because the dataset is small" reasoning (see the Reporting spec's own handler
comments).

- [ ] **Step 5: `Content.CategoryId` + assign endpoint**

`Content.cs` adds `CategoryId` (`Guid?`) and `AssignCategory(Guid? categoryId)` (`MarkUpdated()`
only — no transition guard needed, any status may be recategorized). New command
`AssignContentCategoryCommand(Guid ContentId, Guid? CategoryId)`, handler validates the category
id exists (`categoryRepository.ExistsAsync`) before assigning — 404 on unknown id (`AC-172`).
Controller: `PUT /api/Contents/{id}/category` on `ContentsController`.

- [ ] **Step 6: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentCategoryEndpointTests"`
Expected: PASS, 4/4.

- [ ] **Step 7: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Content/ContentCategory.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ContentCategoryConfiguration.cs \
        backend/src/CustomerSupport.Domain/Entities/Content/Content.cs \
        backend/src/CustomerSupport.Application/Features/Contents/Dtos/ContentDto.cs \
        backend/src/CustomerSupport.Application/Features/ContentCategories/ \
        backend/src/CustomerSupport.InternalApi/Controllers/ContentCategoriesController.cs \
        backend/src/CustomerSupport.InternalApi/Controllers/ContentsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ContentCategoryEndpointTests.cs
git commit -m "feat(kb): category taxonomy, replacing free-text Content.Category (AC-171..174)"
```

---

### Task 4: FAQ curation (`AC-175`, `AC-176`, `AC-177`)

**Files:**
- Modify: `Content.cs` (add `IsFaq`, `MarkAsFaq()`/`UnmarkFaq()`)
- Create: `Features/Contents/Commands/SetFaqFlag/`, `Features/Contents/Queries/GetFaqContents/`
- Modify: `ContentsController.cs`, `KnowledgeBaseController.cs` (public FAQ read)
- Test: `backend/tests/CustomerSupport.Tests/Integration/ContentFaqEndpointTests.cs`

**Interfaces:**
- Consumes: `Content.IsPublished` (already exists).
- Produces: `Content.MarkAsFaq()`/`UnmarkFaq()` — throws `InvalidOperationException` when not
  `IsPublished` (`AC-176`).

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "175")]
public async Task AC175_MarkFaq_PublishedArticle_Succeeds()
{
    var id = await CreatePublishedAsync(); // create + publish helper

    var response = await _client.PutAsJsonAsync($"/api/Contents/{id}/faq", new { isFaq = true });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
[Trait("AC", "176")]
public async Task AC176_MarkFaq_DraftArticle_Returns409()
{
    var id = await CreateDraftAsync();

    var response = await _client.PutAsJsonAsync($"/api/Contents/{id}/faq", new { isFaq = true });

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
[Trait("AC", "177")]
public async Task AC177_FaqEndpoint_ReturnsOnlyFaqArticles()
{
    var faqId = await CreatePublishedAsync();
    await _client.PutAsJsonAsync($"/api/Contents/{faqId}/faq", new { isFaq = true });
    await CreatePublishedAsync(); // not marked FAQ

    var faqs = await _client.GetFromJsonAsync<Response<List<Guid>>>("/api/knowledge-base/articles/faq");
    // project just the ids for the assertion via a minimal DTO, or reuse ContentDto and check Ids

    faqs!.Data.Should().ContainSingle().Which.Should().Be(faqId);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentFaqEndpointTests"`
Expected: FAIL.

- [ ] **Step 3: Domain + command**

```csharp
    public bool IsFaq { get; private set; }

    public void MarkAsFaq()
    {
        if (!IsPublished)
            throw new InvalidOperationException("Only published content may be marked as FAQ.");
        IsFaq = true;
        MarkUpdated();
    }

    public void UnmarkFaq()
    {
        IsFaq = false;
        MarkUpdated();
    }
```

`SetFaqFlagCommand(Guid Id, bool IsFaq)` — handler loads, calls `MarkAsFaq()`/`UnmarkFaq()` per
the flag, catches `InvalidOperationException` → `CONTENT_NOT_PUBLISHABLE` reused from Task 1 (409)
rather than minting a third "not publishable" code for the same underlying rule.

- [ ] **Step 4: FAQ query + both controllers**

`GetFaqContentsQuery` — `contentRepository.ListAsync(c => c.IsFaq && c.Status == "Published", ct)`
(the `Published` filter is defensive: `IsFaq` can only be set on a published article per the
domain guard, but a later un-publish via the generic `UpdateStatus` path could theoretically leave
`IsFaq = true` on a non-published row — filtering here keeps the public endpoint honest either
way). `ContentsController`: `PUT /api/Contents/{id}/faq`. `KnowledgeBaseController`: `GET
/api/knowledge-base/articles/faq`, anonymous, matching the host's existing published-only pattern.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentFaqEndpointTests"`
Expected: PASS, 3/3.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Content/Content.cs \
        backend/src/CustomerSupport.Application/Features/Contents/Commands/SetFaqFlag/ \
        backend/src/CustomerSupport.Application/Features/Contents/Queries/GetFaqContents/ \
        backend/src/CustomerSupport.InternalApi/Controllers/ContentsController.cs \
        backend/src/CustomerSupport.ExternalApi/Controllers/KnowledgeBaseController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ContentFaqEndpointTests.cs
git commit -m "feat(kb): FAQ curation (AC-175, AC-176, AC-177)"
```

---

### Task 5: Article-ticket linking (`AC-178`–`AC-181`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Content/ContentTicketLink.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ContentTicketLinkConfiguration.cs`
- Create: `Features/Contents/Commands/LinkContentToTicket/`, `Commands/UnlinkContentFromTicket/`,
  `Queries/GetLinkedContent/`
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/ContentTicketLinkEndpointTests.cs`

**Interfaces:**
- Produces: `ContentTicketLink(Guid Id, Guid TicketId, Guid ContentId, Guid LinkedByAgentId,
  DateTime LinkedAt)`, `IAppendOnlyEntity`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "178")]
public async Task AC178_LinkPublishedArticle_CreatesRecord()
{
    var ticketId = await CreateTicketAsync();
    var articleId = await CreatePublishedAsync();

    var response = await _client.PostAsync($"/api/Tickets/{ticketId}/content/{articleId}", null);

    response.StatusCode.Should().Be(HttpStatusCode.Created);
}

[Fact]
[Trait("AC", "179")]
public async Task AC179_LinkDraftArticle_Returns409()
{
    var ticketId = await CreateTicketAsync();
    var draftId = await CreateDraftAsync();

    var response = await _client.PostAsync($"/api/Tickets/{ticketId}/content/{draftId}", null);

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
[Trait("AC", "181")]
public async Task AC181_LinkSameArticleTwice_Returns409()
{
    var ticketId = await CreateTicketAsync();
    var articleId = await CreatePublishedAsync();
    await _client.PostAsync($"/api/Tickets/{ticketId}/content/{articleId}", null);

    var response = await _client.PostAsync($"/api/Tickets/{ticketId}/content/{articleId}", null);

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
[Trait("AC", "180")]
public async Task AC180_Unlink_RemovesRecord()
{
    var ticketId = await CreateTicketAsync();
    var articleId = await CreatePublishedAsync();
    await _client.PostAsync($"/api/Tickets/{ticketId}/content/{articleId}", null);

    var response = await _client.DeleteAsync($"/api/Tickets/{ticketId}/content/{articleId}");

    response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    var links = await _client.GetFromJsonAsync<Response<List<Guid>>>($"/api/Tickets/{ticketId}/content");
    links!.Data.Should().BeEmpty();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentTicketLinkEndpointTests"`
Expected: FAIL.

- [ ] **Step 3: Entity + config**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Content/ContentTicketLink.cs
using CustomerSupport.Domain.Entities;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Domain.Entities.Content;

public class ContentTicketLink : BaseEntity, IAppendOnlyEntity
{
    public Guid TicketId { get; private set; }
    public Guid ContentId { get; private set; }
    public Guid LinkedByAgentId { get; private set; }
    public DateTime LinkedAt { get; private set; }

    public static ContentTicketLink Create(Guid ticketId, Guid contentId, Guid linkedByAgentId) => new()
    {
        Id = Guid.NewGuid(),
        TicketId = ticketId,
        ContentId = contentId,
        LinkedByAgentId = linkedByAgentId,
        LinkedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
    };
}
```

EF config: unique index on `(TicketId, ContentId)` (`AC-181`), FKs to `Tickets`/`Contents` with
`DeleteBehavior.Cascade` off the ticket side (removing a ticket should not orphan-fail on its
links) — mirror `TicketHistory`'s FK configuration exactly.

- [ ] **Step 4: Commands, `IDbExceptionTranslator` pairing**

`LinkContentToTicketCommandHandler`: loads the `Content`, checks `content.IsPublished` (else 409
via `CONTENT_NOT_PUBLISHABLE`, reused again), constructs the link, `AddAsync`, catches
`IsUniqueViolation` on save → `LINK_EXISTS` (409) — same `IDbExceptionTranslator` pairing as Task
3's category uniqueness. `UnlinkContentFromTicketCommandHandler`: finds the link by
`(TicketId, ContentId)`, 404 if absent, else deletes. `GetLinkedContentQuery`: lists links for a
ticket, projected with the linked article's title/status (a join, not a second round trip —
`ListProjectedAsync` over the link repository with a nested `Contents` lookup, matching how
`GetTicketsQueryHandler` resolves customer/category names today).

- [ ] **Step 5: Controller actions on `TicketsController`**

`POST /api/Tickets/{ticketId}/content/{contentId}`, `DELETE .../content/{contentId}`,
`GET .../content` — `[Authorize]` (any authenticated staff, matching `US-505`'s "Agent" actor with
no narrower role restriction named in its ACs).

- [ ] **Step 6: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentTicketLinkEndpointTests"`
Expected: PASS, 4/4.

- [ ] **Step 7: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Content/ContentTicketLink.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ContentTicketLinkConfiguration.cs \
        backend/src/CustomerSupport.Application/Features/Contents/Commands/LinkContentToTicket/ \
        backend/src/CustomerSupport.Application/Features/Contents/Commands/UnlinkContentFromTicket/ \
        backend/src/CustomerSupport.Application/Features/Contents/Queries/GetLinkedContent/ \
        backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ContentTicketLinkEndpointTests.cs
git commit -m "feat(kb): article-ticket linking for deflection tracking (AC-178..181)"
```

---

### Task 6: Arabic-aware search (`AC-182`, `AC-183`, `AC-184`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Common/ArabicTextNormalizer.cs`
- Modify: `GetContentsQueryHandler.cs`
- Test: `backend/tests/CustomerSupport.Tests/Unit/ArabicTextNormalizerTests.cs`,
  `backend/tests/CustomerSupport.Tests/Integration/ContentSearchEndpointTests.cs`

**Interfaces:**
- Produces: `static string ArabicTextNormalizer.Fold(string text)` — strips Arabic diacritics
  (tashkeel, U+064B–U+065F range) and common punctuation variants.

- [ ] **Step 1: Write the failing unit test**

```csharp
// backend/tests/CustomerSupport.Tests/Unit/ArabicTextNormalizerTests.cs
using CustomerSupport.Application.Common;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit;

public class ArabicTextNormalizerTests
{
    [Theory]
    [Trait("AC", "182")]
    [InlineData("كِتَاب", "كتاب")]     // diacritics stripped
    [InlineData("كتاب", "كتاب")]        // already bare — idempotent
    [InlineData("hello world", "hello world")] // English untouched — AC-183
    public void Fold_StripsArabicDiacritics(string input, string expected)
    {
        ArabicTextNormalizer.Fold(input).Should().Be(expected);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ArabicTextNormalizerTests"`
Expected: FAIL — class doesn't exist.

- [ ] **Step 3: Implement**

```csharp
// backend/src/CustomerSupport.Application/Common/ArabicTextNormalizer.cs
using System.Text;

namespace CustomerSupport.Application.Common;

/// <summary>AC-182 — folds Arabic diacritics (tashkeel) so "كِتَاب" and "كتاب" compare equal.
/// A no-op on text that carries none, so English search (AC-183) is unaffected.</summary>
public static class ArabicTextNormalizer
{
    // Arabic diacritics block: U+064B–U+065F, plus the superscript alef U+0670.
    private static bool IsDiacritic(char c) => (c >= 'ً' && c <= 'ٟ') || c == 'ٰ';

    public static string Fold(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!IsDiacritic(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
```

- [ ] **Step 4: Run unit test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ArabicTextNormalizerTests"`
Expected: PASS, 3/3.

- [ ] **Step 5: Write the failing integration test**

```csharp
[Fact]
[Trait("AC", "182")]
public async Task AC182_SearchWithoutDiacritics_MatchesArticleWithDiacritics()
{
    await CreatePublishedWithTitleAsync("دليل كِتَاب المستخدم");

    var results = await _client.GetFromJsonAsync<Response<PagedData<ContentDto>>>(
        "/api/knowledge-base/articles?searchTerm=" + Uri.EscapeDataString("كتاب"));

    results!.Data!.Items.Should().NotBeEmpty();
}

[Fact]
[Trait("AC", "184")]
public async Task AC184_SearchNoMatch_ReturnsEmptyListNotError()
{
    var response = await _client.GetAsync(
        "/api/knowledge-base/articles?searchTerm=" + Uri.EscapeDataString("nonexistent-zzz"));

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var results = await response.Content.ReadFromJsonAsync<Response<PagedData<ContentDto>>>();
    results!.Data!.Items.Should().BeEmpty();
}
```

**Important — EF Core translates `string.Contains` to SQL, but `ArabicTextNormalizer.Fold` cannot
be** (it's plain C#, not translatable to a SQL expression). This means the search predicate cannot
call `Fold` inside the LINQ expression tree the way `GetContentsQueryHandler`'s current
`c.Title.Contains(term)` does. **Resolve by folding the search term only, not the stored text**,
and rely on SQL Server's default collation already being diacritic-*insensitive* for combining
marks in practice for many collations — **verify this empirically against the actual test database
before trusting it**; if the default collation does NOT fold diacritics server-side, the fallback
is to fetch a bounded candidate set with a broader (non-diacritic-aware) filter and fold + re-filter
in memory, same trade-off `GetTicketsQueryHandler` already makes for its own in-memory joins. Do
not guess which case applies — run `AC182` against the real `CrmApiFactory` LocalDB and let the
red/green result settle it; write down whichever approach the test actually needed.

- [ ] **Step 6: Run test to verify it fails, then implement per the finding above, then verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentSearchEndpointTests"`

- [ ] **Step 7: Commit**

```bash
git add backend/src/CustomerSupport.Application/Common/ArabicTextNormalizer.cs \
        backend/src/CustomerSupport.Application/Features/Contents/Queries/GetContents/GetContentsQueryHandler.cs \
        backend/tests/CustomerSupport.Tests/Unit/ArabicTextNormalizerTests.cs \
        backend/tests/CustomerSupport.Tests/Integration/ContentSearchEndpointTests.cs
git commit -m "feat(kb): Arabic diacritic-folded search (AC-182, AC-183, AC-184)"
```

---

### Task 7: View tracking (`AC-185`, `AC-186`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Content/ContentView.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ContentViewConfiguration.cs`
- Modify: `backend/src/CustomerSupport.Application/Features/Contents/Queries/GetContentById/GetContentByIdQueryHandler.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/ContentViewTrackingEndpointTests.cs`

**Interfaces:**
- Consumes: `Content.IncrementViewCount()` — **already exists**, currently uncalled by anything.
- Produces: `ContentView(Guid Id, Guid ContentId, Guid? UserId, DateTime ViewedAt)`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "185")]
public async Task AC185_ViewingPublishedArticle_IncrementsViewCount()
{
    var id = await CreatePublishedAsync();
    var before = (await _client.GetFromJsonAsync<Response<ContentDto>>($"/api/knowledge-base/articles/{id}"))!.Data!.ViewCount;

    var after = (await _client.GetFromJsonAsync<Response<ContentDto>>($"/api/knowledge-base/articles/{id}"))!.Data!.ViewCount;

    after.Should().Be(before + 2); // both calls above count — each GET is itself a view
}

[Fact]
[Trait("AC", "186")]
public async Task AC186_AnonymousView_IsCountedWithNullUser()
{
    var id = await CreatePublishedAsync();
    using var anonymous = _factory.CreateClient();

    await anonymous.GetAsync($"/api/knowledge-base/articles/{id}");

    var detail = await _client.GetFromJsonAsync<Response<ContentDto>>($"/api/knowledge-base/articles/{id}");
    detail!.Data!.ViewCount.Should().BeGreaterThanOrEqualTo(1);
}
```

(Note: the first test's own two calls double-count deliberately — matching AC-186's "no
de-duplication this pass," so both requests should register.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentViewTrackingEndpointTests"`
Expected: FAIL — `ViewCount` never changes today, since nothing calls `IncrementViewCount()`.

- [ ] **Step 3: `ContentView` entity + config**

Same shape as `ContentTicketLink` (Task 5) — `Id`, `ContentId`, `UserId` (`Guid?`), `ViewedAt`,
`IAppendOnlyEntity`, FK to `Contents`.

- [ ] **Step 4: Wire into `GetContentByIdQueryHandler`**

```csharp
public class GetContentByIdQueryHandler(
    IRepository<Content> contentRepository,
    IRepository<ContentView> contentViewRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext, // new dependency
    IMessageFactory messages)
    : IQueryHandler<GetContentByIdQuery, Response<ContentDto>>
{
    public async Task<Response<ContentDto>> Handle(GetContentByIdQuery request, CancellationToken ct)
    {
        var content = await contentRepository.GetByIdAsync(request.Id, ct);
        if (content == null)
        {
            return messages.NotFound<ContentDto>(ApplicationErrors.Content.NOT_FOUND);
        }

        if (content.IsPublished)
        {
            content.IncrementViewCount();
            contentRepository.Update(content);
            await contentViewRepository.AddAsync(
                Domain.Entities.Content.ContentView.Create(content.Id, userContext.UserId), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return messages.Success(ToDto(content), ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
```

Only `IsPublished` content increments (a draft viewed via the internal-only `GET
/api/Contents/{id}` — which reuses this same handler per `ContentsController.GetById` — must not
inflate the public view count for content nobody outside staff can see yet). `IUserContext.UserId`
is already nullable-safe for the anonymous `ExternalApi` caller (confirm against its actual
signature — if it throws for an unauthenticated caller instead of returning null, read it via
`HttpContext.User` directly in the handler instead, matching whatever this codebase's anonymous
read paths already do elsewhere, e.g. `KnowledgeBaseController.Ask`'s handler).

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentViewTrackingEndpointTests"`
Expected: PASS, 2/2.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Content/ContentView.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ContentViewConfiguration.cs \
        backend/src/CustomerSupport.Application/Features/Contents/Queries/GetContentById/GetContentByIdQueryHandler.cs \
        backend/tests/CustomerSupport.Tests/Integration/ContentViewTrackingEndpointTests.cs
git commit -m "feat(kb): wire existing ViewCount to real view tracking (AC-185, AC-186)"
```

---

### Task 8: Helpfulness voting (`AC-187`, `AC-188`)

**Files:**
- Create: `backend/src/CustomerSupport.Domain/Entities/Content/ContentVote.cs`
- Create: `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ContentVoteConfiguration.cs`
- Modify: `Content.cs` (add `DislikeCount`, `IncrementDislikeCount`/`DecrementDislikeCount`)
- Create: `Features/Contents/Commands/VoteOnContent/`
- Modify: `KnowledgeBaseController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/ContentVoteEndpointTests.cs`

**Interfaces:**
- Consumes: `Content.IncrementLikeCount()`/`DecrementLikeCount()` — already exist.
- Produces: `Content.IncrementDislikeCount()`/`DecrementDislikeCount()` (new, same shape).
  `ContentVote(Guid Id, Guid ContentId, Guid UserId, bool IsHelpful, DateTime VotedAt)` — **not**
  append-only; upserted in place.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "187")]
public async Task AC187_VoteHelpful_IncrementsLikeCount()
{
    var id = await CreatePublishedAsync();

    var response = await _client.PostAsJsonAsync($"/api/knowledge-base/articles/{id}/vote", new { isHelpful = true });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var detail = await _client.GetFromJsonAsync<Response<ContentDto>>($"/api/knowledge-base/articles/{id}");
    detail!.Data!.LikeCount.Should().Be(1);
}

[Fact]
[Trait("AC", "188")]
public async Task AC188_ChangingVote_MovesCountBetweenColumnsWithoutASecondRow()
{
    var id = await CreatePublishedAsync();
    await _client.PostAsJsonAsync($"/api/knowledge-base/articles/{id}/vote", new { isHelpful = true });

    await _client.PostAsJsonAsync($"/api/knowledge-base/articles/{id}/vote", new { isHelpful = false });

    var detail = await _client.GetFromJsonAsync<Response<ContentDto>>($"/api/knowledge-base/articles/{id}");
    detail!.Data!.LikeCount.Should().Be(0);
    detail.Data.DislikeCount.Should().Be(1);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentVoteEndpointTests"`
Expected: FAIL.

- [ ] **Step 3: `Content.DislikeCount` + `ContentVote` entity**

```csharp
    public int DislikeCount { get; private set; }

    public void IncrementDislikeCount()
    {
        DislikeCount++;
        MarkUpdated();
    }

    public void DecrementDislikeCount()
    {
        if (DislikeCount > 0)
        {
            DislikeCount--;
            MarkUpdated();
        }
    }
```

```csharp
// backend/src/CustomerSupport.Domain/Entities/Content/ContentVote.cs
using CustomerSupport.Domain.Entities;

namespace CustomerSupport.Domain.Entities.Content;

/// <summary>One user's current vote on an article — AC-187/188. Not append-only: a changed vote
/// updates this row in place rather than appending a new one, matching AC-188's "never a second
/// row for the same (ContentId, UserId)."</summary>
public class ContentVote : BaseEntity
{
    public Guid ContentId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsHelpful { get; private set; }
    public DateTime VotedAt { get; private set; }

    public static ContentVote Create(Guid contentId, Guid userId, bool isHelpful) => new()
    {
        Id = Guid.NewGuid(),
        ContentId = contentId,
        UserId = userId,
        IsHelpful = isHelpful,
        VotedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
    };

    public void ChangeTo(bool isHelpful)
    {
        IsHelpful = isHelpful;
        VotedAt = DateTime.UtcNow;
        MarkUpdated();
    }
}
```

EF config: unique index on `(ContentId, UserId)`.

- [ ] **Step 4: `VoteOnContentCommand` — upsert semantics**

```csharp
public class VoteOnContentCommandHandler(
    IRepository<Content> contentRepository,
    IRepository<ContentVote> voteRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IMessageFactory messages)
    : ICommandHandler<VoteOnContentCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(VoteOnContentCommand request, CancellationToken ct)
    {
        var content = await contentRepository.GetByIdAsync(request.ContentId, ct);
        if (content == null || !content.IsPublished)
        {
            return messages.NotFound<Unit>(ApplicationErrors.Content.NOT_FOUND);
        }

        var existing = (await voteRepository.ListAsync(
            v => v.ContentId == request.ContentId && v.UserId == userContext.UserId!.Value, ct))
            .SingleOrDefault();

        if (existing == null)
        {
            await voteRepository.AddAsync(
                Domain.Entities.Content.ContentVote.Create(request.ContentId, userContext.UserId!.Value, request.IsHelpful), ct);
            if (request.IsHelpful) content.IncrementLikeCount(); else content.IncrementDislikeCount();
        }
        else if (existing.IsHelpful != request.IsHelpful)
        {
            existing.ChangeTo(request.IsHelpful);
            voteRepository.Update(existing);
            if (request.IsHelpful)
            {
                content.IncrementLikeCount();
                content.DecrementDislikeCount();
            }
            else
            {
                content.IncrementDislikeCount();
                content.DecrementLikeCount();
            }
        }
        // else: same vote resubmitted — no count change, no error (idempotent).

        contentRepository.Update(content);
        await unitOfWork.SaveChangesAsync(ct);
        return messages.Success(Unit.Value, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
```

`VoteOnContentCommand(Guid ContentId, bool IsHelpful)`. Controller: `POST
/api/knowledge-base/articles/{id}/vote` on `KnowledgeBaseController` — **not** `[AllowAnonymous]**
despite the rest of that controller being anonymous: `AC-188`'s per-user upsert requires an
identity, so this one action needs `[Authorize]`, layered onto the controller's otherwise-anonymous
default via an explicit attribute on the action (matching how `[AllowAnonymous]` itself is already
applied at the action level for `Ask` even though the controller carries it at the class level too
— the pattern for a per-action override already exists in this file).

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~ContentVoteEndpointTests"`
Expected: PASS, 2/2.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Domain/Entities/Content/Content.cs \
        backend/src/CustomerSupport.Domain/Entities/Content/ContentVote.cs \
        backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ContentVoteConfiguration.cs \
        backend/src/CustomerSupport.Application/Features/Contents/Commands/VoteOnContent/ \
        backend/src/CustomerSupport.ExternalApi/Controllers/KnowledgeBaseController.cs \
        backend/tests/CustomerSupport.Tests/Integration/ContentVoteEndpointTests.cs
git commit -m "feat(kb): helpfulness voting, one vote per user (AC-187, AC-188)"
```

---

### Task 9: Migration and full-suite gate

**Files:**
- Create: one EF Core migration covering Tasks 1–8's combined schema changes.
- Modify: `ContentDto.cs` to include every new field the frontend plan (written after this
  backend lands) will need: `Version`, `IsFaq`, `DislikeCount`, `CategoryId`, `CategoryName`.

- [ ] **Step 1: Generate the migration**

Run: `cd backend && dotnet ef migrations add AddKnowledgeBaseFeatures --project src/CustomerSupport.Infrastructure --startup-project src/CustomerSupport.InternalApi`

- [ ] **Step 2: Review the migration before it is ever applied**

Read the generated `.cs` file in full. Check specifically: every new unique index from Tasks 3/5/8
is present with the right column set; `ContentCategory.ParentId`'s FK is `Restrict`, not
`Cascade`; `Content.Version`/`DislikeCount`/`IsFaq` all default correctly for existing rows
(`Version` defaults to `1`, matching every pre-existing `Content` row's true version — not `0`).
This is the exact review step that caught the `EscalationState` default-value bug in `FEAT-17`
(`docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-escalation/README.md`) — do not skip it.

- [ ] **Step 3: Build and run the full new-test surface**

Run: `cd backend && dotnet build CustomerSupport.slnx`
Expected: Build succeeded, 0 errors, 0 new warnings.

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Content"`
Expected: PASS — every test file from Tasks 1–8.

Run: `cd backend && dotnet test CustomerSupport.slnx`
Expected: PASS, full suite, no regressions. Paste the actual summary line.

- [ ] **Step 4: Commit**

```bash
git add backend/src/CustomerSupport.Infrastructure/Migrations/ \
        backend/src/CustomerSupport.Application/Features/Contents/Dtos/ContentDto.cs
git commit -m "feat(kb): migration for all knowledge-base schema changes"
```

## Definition of done

`AC-165` through `AC-188` (backend scope) each covered by a test naming it · `dotnet build` clean,
0 new warnings · `dotnet test CustomerSupport.slnx` green, full output pasted into the task record
· task record written to `docs/superpowers/plans/EPIC-06-US-504-feat-11-knowledge-base/README.md`.

**Frontend (`AC-189`–`AC-195`, `US-509`–`US-513`) is a separate plan, written after this backend is
actually implemented and green** — per this project's standing rule that the frontend plan follows
backend implementation, not the spec-writing pass. Not written in this session, since this session
was directed to produce specs and backend plans only, not implement.
