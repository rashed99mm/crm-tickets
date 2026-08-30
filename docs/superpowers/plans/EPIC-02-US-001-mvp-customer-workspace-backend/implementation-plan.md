# MVP-05 — Interaction history · **backend** implementation plan

**Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.**

**Date:** 2026-08-26
**Spec:** [`../../specs/EPIC-02-US-001-customer-workspace.md`](../../specs/EPIC-02-US-001-customer-workspace.md)
**Criteria:** `AC-74`, `AC-75`, `AC-76` · legacy `AC-17`…`AC-21`
**Runs in parallel with:** the frontend plan (`EPIC-02-US-001-mvp-customer-workspace-frontend`). Disjoint
files; the DTO shape is the meeting point.

## What already exists

`CustomerNote` (`backend/src/CustomerSupport.Domain/Entities/Customers/CustomerNote.cs`) — a
`BaseEntity` whose `Create` factory takes `authorId` as a required constructor argument with no
setter, so no shape of this entity can carry an author supplied by a request body. `CustomerNotes`
table with `IX_CustomerNotes_Customer_Created`.

## Global Constraints

- `EveryErrorCode_HasABilingualMessage` fails the build if a new `ApplicationErrors` key has no
  matching `Resources.yaml` pair — add both in the same commit as the code that introduces the key.

---

### Task 1: Add a note (`AC-75`, `AC-76`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Customers/Dtos/CustomerNoteDtos.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Customers/Commands/AddCustomerNote/AddCustomerNoteCommand.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Customers/Commands/AddCustomerNote/AddCustomerNoteCommandHandler.cs`
- Modify: `backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs`, `Resources.yaml`
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/CustomersController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/CustomerNotesEndpointTests.cs`

**Interfaces:**
- Produces: `AddCustomerNoteCommand(Guid CustomerId, string Body) : ICommand<Response<Guid>>`.
  Deliberately **no** `AuthorId` parameter — `AC-76`'s whole defence is that the field does not
  exist to be honoured by accident.

- [ ] **Step 1: Write the failing test**

```csharp
// The one that matters — a body that CONTAINS an authorId, proving the handler ignores it.
[Fact]
[Trait("AC", "76")]
public async Task AC76_AddNote_AuthorComesFromTheTokenNotThePayload()
{
    var otherUser = await _factory.CreateAuthenticatedClientAsync();
    var response = await _client.PostAsJsonAsync($"/api/Customers/{_customerId}/notes",
        new { body = "Injected", authorId = otherUser.Item2.Id }); // extra field, must be ignored

    response.StatusCode.Should().Be(HttpStatusCode.Created);
    var notes = await _client.GetFromJsonAsync<Response<PagedData<NoteRow>>>($"/api/Customers/{_customerId}/notes");
    var note = notes!.Data!.Items.Single(n => n.Body == "Injected");
    note.AuthorId.Should().Be(_callerId);
    note.AuthorId.Should().NotBe(otherUser.Item2.Id);
}

[Fact]
[Trait("AC", "75")]
public async Task AC75_AddNote_EmptyBody_Returns400KeyedToBody()
{
    var response = await _client.PostAsJsonAsync($"/api/Customers/{_customerId}/notes", new { body = "" });
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var body = await response.Content.ReadFromJsonAsync<Response<object>>();
    body!.Errors.Should().Contain(e => e.Field == "Body");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CustomerNotesEndpointTests"`
Expected: FAIL — route doesn't exist.

- [ ] **Step 3: DTOs**

```csharp
// CustomerNoteDtos.cs
namespace CustomerSupport.Application.Features.Customers.Dtos;

/// One interaction record. AuthorName is projected at read time — the row stores AuthorId only.
public record CustomerNoteDto(Guid Id, string Body, Guid AuthorId, string AuthorName, DateTime CreatedAt);

/// The create payload. No author field — AC-76.
public record CreateCustomerNoteRequest(string Body);
```

- [ ] **Step 4: Command + handler**

```csharp
// AddCustomerNoteCommand.cs
public record AddCustomerNoteCommand(Guid CustomerId, string Body) : ICommand<Response<Guid>>;
```

```csharp
// AddCustomerNoteCommandHandler.cs
public class AddCustomerNoteCommandHandler(
    IRepository<CustomerNote> notes,
    IRepository<Customer> customers,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<AddCustomerNoteCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(AddCustomerNoteCommand request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(c => c.Id == request.CustomerId, ct))
        {
            return messages.NotFound<Guid>(ApplicationErrors.Customer.NOT_FOUND);
        }

        var note = CustomerNote.Create(request.CustomerId, request.Body, userContext.UserId);

        await notes.AddAsync(note, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(note.Id, ApplicationErrors.Customer.NOTE_ADDED);
    }
}
```

- [ ] **Step 5: Error codes**

`ApplicationErrors.Customer` → `NOTE_ADDED = "CUSTOMER_NOTE_ADDED"`. `ApplicationErrors.Validation`
→ `NOTE_BODY_REQUIRED`, `NOTE_BODY_MAX_LENGTH`. Bilingual pair in `Resources.yaml` for each.

- [ ] **Step 6: Controller action**

```csharp
[HttpPost("{id:guid}/notes")]
[ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
public async Task<IActionResult> AddNote(Guid id, [FromBody] CreateCustomerNoteRequest request, CancellationToken ct)
{
    var result = await _mediator.Send(new AddCustomerNoteCommand(id, request.Body), ct);
    return this.ToActionResult(result, StatusCodes.Status201Created);
}
```

- [ ] **Step 7: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC76_AddNote_AuthorComesFromTheTokenNotThePayload|FullyQualifiedName~AC75_AddNote_EmptyBody_Returns400KeyedToBody"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Customers/Dtos/CustomerNoteDtos.cs backend/src/CustomerSupport.Application/Features/Customers/Commands/AddCustomerNote/ backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml backend/src/CustomerSupport.InternalApi/Controllers/CustomersController.cs backend/tests/CustomerSupport.Tests/Integration/CustomerNotesEndpointTests.cs
git commit -m "feat(customers): record a note against a customer (AC-75, AC-76)"
```

---

### Task 2: Read notes, newest first (`AC-74`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Customers/Queries/GetCustomerNotes/GetCustomerNotesQuery.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Customers/Queries/GetCustomerNotes/GetCustomerNotesQueryHandler.cs`
- Modify: `CustomersController.cs`
- Test: same file as Task 1

**Interfaces:**
- Produces: `GetCustomerNotesQuery : BasePagedQuery, IQuery<Response<PaginatedList<CustomerNoteDto>>>` with `Guid CustomerId`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "74")]
public async Task AC74_GetNotes_ReturnsNewestFirstWithAuthorNames()
{
    await AddNoteAsync("First");
    await Task.Delay(20);
    await AddNoteAsync("Second");

    var page = await _client.GetFromJsonAsync<Response<PagedData<NoteRow>>>($"/api/Customers/{_customerId}/notes");

    page!.Data!.Items.First().Body.Should().Be("Second");
    page.Data.Items.All(n => !string.IsNullOrEmpty(n.AuthorName)).Should().BeTrue();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC74_GetNotes_ReturnsNewestFirstWithAuthorNames"`
Expected: FAIL — route doesn't exist.

- [ ] **Step 3: Query + handler**

```csharp
// GetCustomerNotesQuery.cs
public class GetCustomerNotesQuery : BasePagedQuery, IQuery<Response<PaginatedList<CustomerNoteDto>>>
{
    public Guid CustomerId { get; init; }
}
```

```csharp
// GetCustomerNotesQueryHandler.cs — 404 if the customer is absent, otherwise newest-first,
// author names resolved once per distinct author via IIdentityUserService (ApplicationUser is
// outside IRepository<T>'s BaseEntity constraint, the same arrangement GetTicketByIdQuery uses).
public class GetCustomerNotesQueryHandler(
    IRepository<CustomerNote> notes,
    IRepository<Customer> customers,
    IIdentityUserService identityUsers,
    IMessageFactory messages)
    : IQueryHandler<GetCustomerNotesQuery, Response<PaginatedList<CustomerNoteDto>>>
{
    public async Task<Response<PaginatedList<CustomerNoteDto>>> Handle(
        GetCustomerNotesQuery request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(c => c.Id == request.CustomerId, ct))
        {
            return messages.NotFound<PaginatedList<CustomerNoteDto>>(ApplicationErrors.Customer.NOT_FOUND);
        }

        var pageIndex = Math.Max(request.PageIndex, 1);
        var pageSize = Math.Max(request.PageSize, 1);

        var ordered = (await notes.ListAsync(n => n.CustomerId == request.CustomerId, ct))
            .OrderByDescending(n => n.CreatedAt).ToList();
        var total = ordered.Count;
        var rows = ordered.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();

        var authorNames = new Dictionary<Guid, string>();
        foreach (var authorId in rows.Select(n => n.AuthorId).Distinct())
        {
            var author = await identityUsers.FindByIdAsync(authorId, ct);
            authorNames[authorId] = author?.FullName ?? string.Empty;
        }

        var items = rows.Select(n => new CustomerNoteDto(
            n.Id, n.Body, n.AuthorId, authorNames.GetValueOrDefault(n.AuthorId, string.Empty), n.CreatedAt)).ToList();

        return Response<PaginatedList<CustomerNoteDto>>.Ok(
            PaginatedList<CustomerNoteDto>.Create(items, total, pageIndex, pageSize),
            SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
```

- [ ] **Step 4: Controller action**

```csharp
[HttpGet("{id:guid}/notes")]
public async Task<IActionResult> GetNotes(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
{
    var result = await _mediator.Send(new GetCustomerNotesQuery { CustomerId = id, PageIndex = page, PageSize = pageSize }, ct);
    return this.ToActionResult(result);
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CustomerNotesEndpointTests"`
Expected: PASS, all tests in the file.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Customers/Queries/GetCustomerNotes/ backend/src/CustomerSupport.InternalApi/Controllers/CustomersController.cs backend/tests/CustomerSupport.Tests/Integration/CustomerNotesEndpointTests.cs
git commit -m "feat(customers): read notes newest first, with author names (AC-74)"
```

## Definition of done

`AC-74`, `AC-75`, `AC-76` each covered by a test naming it · `dotnet test` green with output pasted
· 0 build errors, no new warnings. **Frontend owns `frontend/` — not touched here.**
