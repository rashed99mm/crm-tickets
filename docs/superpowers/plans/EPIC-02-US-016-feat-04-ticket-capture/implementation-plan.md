# FEAT-04 Ticket Capture (backend) Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** Raise a tracked ticket against a customer — `TKT-nnnnnn` reference, status `New`, no assignee — with field-keyed validation, plus the category seeder (`AC-29`..`AC-31`, `BASE-11`, assumption `A4`).

**Spec:** `docs/superpowers/specs/EPIC-02-US-016-ticket-lifecycle.md` (`AC-29`..`AC-31`).

**Architecture:** `Ticket.Create` (Domain) → `CreateTicketCommandHandler` (Application) → `TicketsController.Create`. `ITicketReferenceGenerator` issues the reference from the `TicketReferenceSequence` in `AppDbContext`. SLA targets are resolved at creation by `ApplySlaTargetsAsync`.

## Global constraints

- Unknown `customerId`/`categoryId` → **400 keyed to that field**, not 404 (`AC-31`): the ticket collection exists, the payload is wrong.
- The ticket starts `New` with `AssigneeId = null` and a generated reference (`AC-29`).
- `CategorySeeder` is internal-host only, idempotent, fixed four-bucket list.

## Task 1 — `Ticket.Create` + reference generator (`AC-29`)

**Files:**
- `backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs` (`Create`)
- `backend/src/CustomerSupport.Application/Interfaces/ITicketReferenceGenerator.cs`
- `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs` (`TicketReferenceSequence`)

**Interfaces:** `ITicketReferenceGenerator.NextAsync(CancellationToken) : Task<string>` — backed by `NEXT VALUE FOR TicketReferenceSequence`.

**Step 1 — Real domain Create (excerpt)**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs
public static Ticket Create(string reference, string subject, string description,
    Guid customerId, Guid categoryId, string priority, Guid actorId)
{
    if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Reference is required", nameof(reference));
    if (string.IsNullOrWhiteSpace(subject) || subject.Length > 200)
        throw new ArgumentException("Subject must not exceed 200 characters", nameof(subject));
    if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required", nameof(description));
    if (customerId == Guid.Empty) throw new ArgumentException("A customer is required", nameof(customerId));
    if (categoryId == Guid.Empty) throw new ArgumentException("A category is required", nameof(categoryId));
    if (actorId == Guid.Empty) throw new ArgumentException("An actor is required", nameof(actorId));

    var ticket = new Ticket
    {
        Id = Guid.NewGuid(), Reference = reference.Trim(), Subject = subject.Trim(),
        Description = description, CustomerId = customerId, CategoryId = categoryId,
        Priority = TicketPriority.Create(priority).Value, Status = TicketStatus.New.Value,
        AssigneeId = null, CreatedAt = DateTime.UtcNow, CreatedBy = actorId
    };
    ticket.Append(actorId, TicketChangeType.Created, null, ticket.Status);   // first history row
    ticket.AddDomainEvent(new TicketCreatedEvent(ticket.Id, ticket.Reference, ticket.CustomerId, actorId));
    return ticket;
}
```

`Reference` is a sequence (`StartsAt(1000)`), not `MAX(Reference)+1` — the latter races under concurrent inserts and the unique index would 500.

- [ ] **Step 2: Run — domain tests**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~TicketTests"`
Expected: PASS — `Create` sets `New`, null assignee, appends a `Created` history row.

- [ ] **Step 3: Commit:** `git add backend/src/CustomerSupport.Domain/Entities/Tickets/Ticket.cs backend/src/CustomerSupport.Application/Interfaces/ITicketReferenceGenerator.cs && git commit -m "feat(tickets): Ticket.Create + reference generator (AC-29)"`

## Task 2 — CreateTicket handler: field-keyed validation (`AC-30`, `AC-31`)

**Files:** `backend/src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/{CreateTicketCommand,CreateTicketCommandHandler,CreateTicketCommandValidator}.cs`

**Interfaces:** `CreateTicketCommand(string Subject, string Description, Guid CustomerId, Guid CategoryId, string Priority) : ICommand<Response<Guid>>`.

**Step 1 — Real handler (excerpt)**

```csharp
// backend/src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/CreateTicketCommandHandler.cs
public class CreateTicketCommandHandler(
    IRepository<Ticket> tickets, IRepository<Customer> customers, IRepository<Category> categories,
    IRepository<SLAPolicy> slaPolicies, ITicketReferenceGenerator references,
    IUserContext userContext, IUnitOfWork unitOfWork, IMessageFactory messages)
    : ICommandHandler<CreateTicketCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateTicketCommand request, CancellationToken ct)
    {
        var missing = new List<FieldError>();
        if (!await customers.ExistsAsync(c => c.Id == request.CustomerId, ct))
            missing.Add(new FieldError("CustomerId",
                SystemCodeMap.Resolve(ApplicationErrors.Ticket.CUSTOMER_NOT_FOUND),
                ApplicationErrors.Ticket.CUSTOMER_NOT_FOUND));
        if (!await categories.ExistsAsync(c => c.Id == request.CategoryId && c.IsActive, ct))
            missing.Add(new FieldError("CategoryId",
                SystemCodeMap.Resolve(ApplicationErrors.Ticket.CATEGORY_NOT_FOUND),
                ApplicationErrors.Ticket.CATEGORY_NOT_FOUND));
        if (missing.Count > 0)
            return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR, missing);

        var reference = await references.NextAsync(ct);
        var ticket = Ticket.Create(reference, request.Subject, request.Description,
            request.CustomerId, request.CategoryId, request.Priority, userContext.UserId);
        await ApplySlaTargetsAsync(ticket, ct);
        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return messages.Success(ticket.Id, ApplicationErrors.Ticket.CREATED);
    }
}
```

The `FieldError("CustomerId", …)` is exactly what lets the Angular create form bind the server rejection to the right control (`AC-60`).

- [ ] **Step 2: Run — integration test**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CreateTicketEndpointTests"`
Expected: PASS — valid → 201; unknown `customerId` → 400 keyed `CustomerId`; unknown `categoryId` → 400 keyed `CategoryId`.

- [ ] **Step 3: Commit:** `git add backend/src/CustomerSupport.Application/Features/Tickets/Commands/CreateTicket/ && git commit -m "feat(tickets): create handler, field-keyed 400 (AC-30, AC-31)"`

## Task 3 — Controller create + category seeder (`AC-29`, `BASE-11`, `A4`)

**Files:**
- `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs` (`Create`)
- `backend/src/CustomerSupport.Infrastructure/Seeders/CategorySeeder.cs`
- `backend/src/CustomerSupport.InternalApi/Controllers/CategoriesController.cs` (`GetCategories`)

**Step 1 — Real controller create (excerpt)**

```csharp
[HttpPost]
[ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> Create([FromBody] CreateTicketRequest request, CancellationToken ct)
{
    var result = await mediator.Send(new CreateTicketCommand(
        request.Subject, request.Description, request.CustomerId, request.CategoryId, request.Priority), ct);
    if (!result.Success) return this.ToActionResult(result);
    return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
}
```

**Step 2 — Real seeder (excerpt)**

```csharp
// backend/src/CustomerSupport.Infrastructure/Seeders/CategorySeeder.cs
public static readonly string[] Names = ["Technical", "Billing", "Account", "General"]; // fixed four (A4)
public async Task SeedAsync(CancellationToken ct = default)
{
    var missing = await MissingNamesAsync(ct);
    if (missing.Count == 0) return;
    foreach (var name in missing) db.Categories.Add(Category.Create(name));
    try { await db.SaveChangesAsync(ct); }
    catch (DbUpdateException)            // lose the race on a rolling deploy? rows are there; carry on
    {
        foreach (var e in db.ChangeTracker.Entries<Category>().ToList()) e.State = EntityState.Detached;
        if ((await MissingNamesAsync(ct)).Count > 0) throw;
    }
}
```

`CategoriesController.GetCategories` exposes the seeded list to the create form's category picker (`ticket.api.listCategories`).

- [ ] **Step 3: Run — seeder + create E2E**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CategorySeederTests"`
Expected: PASS — four categories present after `UsePlatformDataSeedingAsync`; create succeeds with one of them.

- [ ] **Step 4: Commit:** `git add backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs backend/src/CustomerSupport.Infrastructure/Seeders/CategorySeeder.cs backend/src/CustomerSupport.InternalApi/Controllers/CategoriesController.cs && git commit -m "feat(tickets): create endpoint + category seeder (AC-29, BASE-11)"`

## Self-review

Coverage: `AC-29` → Tasks 1,3; `AC-30`,`AC-31` → Task 2; `BASE-11`,`A4` → Task 3.

**Discrepancy found:** the old plan framed the create-screen as "the first thing that proves the envelope `errors[]` contract is consumable" and put the category seeder as a late task. The shipped code seeds categories on every internal-host start (idempotent) and the field-keyed `FieldError` already flows to the form — matches the plan's intent, no behavioral gap.
