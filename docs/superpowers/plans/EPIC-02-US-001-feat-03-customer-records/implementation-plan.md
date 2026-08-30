# FEAT-03 Customer Records Implementation Plan

> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan did not precede its implementation.

**Goal:** Record, find, correct and (guarded) delete the people who contact support — `AC-7`..`AC-16` (and later `AC-69`..`AC-76` notes/attachments, covered by the inherited surface).

**Spec:** `docs/superpowers/specs/EPIC-02-US-016-ticket-lifecycle.md` (`AC-7`..`AC-16`).

**Architecture:** `Customer` aggregate (Domain) → `Customers/*` CQRS in Application → `CustomersController` (InternalApi). Uniqueness of email is a filtered unique index, not an entity rule (ADR-0006).

## Global constraints

- No role policy beyond `Authenticated` on `CustomersController` — the slice places no role restriction on customer management.
- Duplicate email → **409**, not 400 (`AC-9`): the request is well-formed; the world refuses it.
- Delete guard → **409** `CUSTOMER_HAS_TICKETS` if the customer holds any ticket; otherwise soft-delete → 200 (`AC-15`,`AC-16`).

## Task 1 — `Customer` entity + validation (`AC-7`, `AC-8`, `AC-14`)

**Files:** `backend/src/CustomerSupport.Domain/Entities/Customers/Customer.cs`

**Interfaces:** `Customer.Create(name, email, phone)`, `Customer.Update(name, email, phone)` — both validate identically.

**Step 1 — Real entity (excerpt)**

```csharp
// backend/src/CustomerSupport.Domain/Entities/Customers/Customer.cs
public partial class Customer : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;   // stored lower-cased
    public string? Phone { get; private set; }

    public static Customer Create(string name, string email, string? phone)
    {
        var (validName, validEmail) = Validate(name, email);
        return new Customer
        {
            Id = Guid.NewGuid(), Name = validName, Email = validEmail,
            Phone = Normalise(phone), CreatedAt = DateTime.UtcNow
        };
    }

    private static (string Name, string Email) Validate(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required", nameof(name));
        if (name.Length > 200) throw new ArgumentException("Name must not exceed 200 characters", nameof(name));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required", nameof(email));
        var trimmed = email.Trim();
        if (trimmed.Length > 320) throw new ArgumentException("Email must not exceed 320 characters", nameof(email));
        if (!EmailPattern().IsMatch(trimmed)) throw new ArgumentException($"Invalid email address: {email}", nameof(email));
        return (name.Trim(), trimmed.ToLowerInvariant());   // UX_Customers_Email catches case variants
    }

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
```

- [ ] **Step 2: Run — domain tests**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CustomerTests"`
Expected: PASS — empty name, bad email, >200 chars all throw.

- [ ] **Step 3: Commit:** `git add backend/src/CustomerSupport.Domain/Entities/Customers/Customer.cs && git commit -m "feat(customers): Customer aggregate + validation (AC-7, AC-8, AC-14)"`

## Task 2 — Create + duplicate-email 409 (`AC-9`)

**Files:**
- `backend/src/CustomerSupport.Application/Features/Customers/Commands/CreateCustomer/{CreateCustomerCommand,CreateCustomerCommandHandler,CreateCustomerCommandValidator}.cs`
- `backend/src/CustomerSupport.InternalApi/Controllers/CustomersController.cs` (`Create`)

**Interfaces:** `CreateCustomerCommand(string Name, string Email, string? Phone) : ICommand<Response<Guid>>`.

**Step 1 — Real handler (excerpt)**

```csharp
// backend/src/CustomerSupport.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs
public class CreateCustomerCommandHandler(
    IRepository<Customer> customers, IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator, IMessageFactory messages)
    : ICommandHandler<CreateCustomerCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        var normalisedEmail = request.Email.Trim().ToLowerInvariant();
        if (await customers.ExistsAsync(c => c.Email == normalisedEmail, ct))
            return messages.Fail<Guid>(ApplicationErrors.Customer.EMAIL_EXISTS, MessageType.Conflict);
        var customer = Customer.Create(request.Name, request.Email, request.Phone);
        await customers.AddAsync(customer, ct);
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Customer.EMAIL_EXISTS, MessageType.Conflict);
        }
        return messages.Success(customer.Id, ApplicationErrors.Customer.CREATED);
    }
}
```

The `IDbExceptionTranslator` pair is the recurring lesson (FEAT-16): the unique index must 409, not 500, on a race the `ExistsAsync` check missed.

**Step 2 — Controller returns 201 + Location** (real): `Create` maps `!result.Success` to `ToActionResult`, else `CreatedAtAction(nameof(GetById), new { id = result.Data }, result)` so `AC-7`'s Location header is satisfied.

- [ ] **Step 3: Run — integration test**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CustomerEndpointTests"`
Expected: PASS — create → 201; duplicate email → 409 `EMAIL_EXISTS`.

- [ ] **Step 4: Commit:** `git add backend/src/CustomerSupport.Application/Features/Customers/Commands/CreateCustomer/ backend/src/CustomerSupport.InternalApi/Controllers/CustomersController.cs && git commit -m "feat(customers): create + 409 duplicate email (AC-9)"`

## Task 3 — List, get-by-id, update, delete guard (`AC-10`..`AC-16`)

**Files:** `Features/Customers/Queries/GetCustomers/*`, `GetCustomerById/*`, `Commands/UpdateCustomer/*`, `DeleteCustomer/*`, `CustomersController`.

**Step 1 — Controller surface (real)**

```csharp
[HttpGet]                                  // paged + search; pageSize over max -> 400 (AC-11)
public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, CancellationToken ct = default)
    => this.ToActionResult(await mediator.Send(new GetCustomersQuery { PageIndex = page, PageSize = pageSize, Search = search }, ct));

[HttpGet("{id:guid}")]                     // unknown/deleted -> 404 (AC-12)
public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    => this.ToActionResult(await mediator.Send(new GetCustomerByIdQuery(id), ct));

[HttpPut("{id:guid}")]                     // same validation as create (AC-14); dup email -> 409
public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct = default)
    => this.ToActionResult(await mediator.Send(new UpdateCustomerCommand(id, request.Name, request.Email, request.Phone), ct));

[HttpDelete("{id:guid}")]                  // ticket guard -> 409; else soft-delete -> 200 (AC-15, AC-16)
public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    => this.ToActionResult(await mediator.Send(new DeleteCustomerCommand(id), ct));
```

`GetCustomersQueryHandler` projects to `CustomerDto` with an in-memory search over `Name`/`Email` (case-insensitive, `AC-13`); `pageSize` over the server maximum is a 400.

- [ ] **Step 2: Run:** `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CustomerListTests"`
Expected: PASS — paged, search, delete guard 409.

- [ ] **Step 3: Commit:** `git add backend/src/CustomerSupport.Application/Features/Customers/ backend/src/CustomerSupport.InternalApi/Controllers/CustomersController.cs && git commit -m "feat(customers): list/get/update/delete guard (AC-10..AC-16)"`

## Self-review

Coverage: `AC-7`,`AC-8`,`AC-14` → Task 1; `AC-9` → Task 2; `AC-10`..`AC-13`,`AC-15`,`AC-16` → Task 3.

**Discrepancy found:** the old plan claimed "delete → 409 `ERR012`, soft-deleted, returns **200**". The shipped `DeleteCustomer` maps to 200 and reuses the `CUSTOMER_HAS_TICKETS`/`EMAIL_EXISTS`-style domain key set; the exact wire code is `ERR012`-equivalent only if registered in `SystemCodeMap` — the rewrite references the actual `ApplicationErrors.Customer.*` keys rather than assuming `ERR012` literally.
