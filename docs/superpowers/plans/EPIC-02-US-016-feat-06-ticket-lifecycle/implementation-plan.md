> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# FEAT-06 — Ticket detail and lifecycle · backend plan

> **Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan
> did not precede its implementation.**

**Date:** 2026-08-26
**Feature:** `FEAT-06`, sprint 3, **vertical** · backend half · 25 points
**Spec:** `AC-35`…`AC-41`, `BASE-12`
**Frontend counterpart:** [`EPIC-02-US-016-feat-06-ticket-detail-frontend.md`](../EPIC-02-US-016-feat-06-ticket-detail-frontend/implementation-plan.md)
— `US-128`, which also closes `FEAT-07` and `FEAT-08`'s user surface, so it is written after all
three backend plans complete
**Depends on:** `FEAT-04` (a ticket must exist), Phase 0 (the aggregate and its transition table)

## What already exists, and what that changes

Phase 0 delivered `Ticket.ChangeStatus`, the closed transition table in `TicketStatus`, the reopen
distinction, and unit tests over them. `FEAT-04` delivered `GetTicketByIdQuery` — recorded there
as task 6, which noted it implements `AC-35` and `AC-50` **ahead of their feature and without
claiming them**.

So the domain half of this feature is done and tested. **What remains is the handler, the wire and
the concurrency half**, plus claiming `AC-35` with a test that names it.

---

### Task 1: Claim `AC-35`/`AC-36` over the existing `GetTicketByIdQuery`

**Files:**
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

**Interfaces:**
- Consumes: `GetTicketByIdQueryHandler` (`CustomerSupport.Application/Features/Tickets/Queries/GetTicketById/`)
  — already returns `TicketDetailDto` with `Customer`, `History` (newest-first), `RowVersion`
  base64-encoded. No code changes this task — it is a coverage claim, not new behavior.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "35")]
public async Task AC35_GetTicket_ReturnsCustomerSummaryAndHistoryNewestFirst()
{
    var ticketId = await CreateTicketAsync("Cannot sign in");

    var detail = await _client.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    detail!.Data!.Customer.Name.Should().NotBeNullOrEmpty();
    detail.Data.History.Should().ContainSingle(h => h.ChangeType == "Created");
}

public sealed record TicketDetailRow(CustomerRow Customer, List<HistoryRow> History, string RowVersion);
public sealed record CustomerRow(string Name);
public sealed record HistoryRow(string ChangeType);
```

- [ ] **Step 2: Run to verify it passes without any implementation change**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC35_GetTicket"`
Expected: PASS immediately — this is claiming an already-correct behavior with a named test, not
building new behavior. If it fails, the assumption that `AC-35` already works is wrong and this
task stops being a claim and becomes a fix.

- [ ] **Step 3: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "test(tickets): claim AC-35 over the existing GetTicketByIdQuery"
```

---

### Task 2: `ChangeTicketStatusCommand` — move the ticket along the lifecycle (`AC-37`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommand.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandHandler.cs`
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

**Interfaces:**
- Consumes: `Ticket.ChangeStatus(string targetStatus, Guid actorId)` (already exists,
  `CustomerSupport.Domain/Entities/Tickets/Ticket.cs`) — throws `InvalidOperationException` on an
  illegal transition via `TicketStatus.CanTransitionTo`.
- Produces: `ChangeTicketStatusCommand(Guid TicketId, string Status, string RowVersion) :
  ICommand<Response<Guid>>`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "37")]
public async Task AC37_ChangeStatus_PermittedTransition_Returns200AndPersists()
{
    var ticketId = await CreateTicketAsync("Needs triage");
    var before = await _client.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    var response = await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = "Open", rowVersion = before!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var after = await _client.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
    after!.Data!.Status.Should().Be("Open");
}

public sealed record TicketRow(string Status);
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC37_ChangeStatus"`
Expected: FAIL — 404, `POST /api/Tickets/{id}/status` doesn't exist yet.

- [ ] **Step 3: Command + handler**

```csharp
// ChangeTicketStatusCommand.cs
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.ChangeTicketStatus;

public record ChangeTicketStatusCommand(Guid TicketId, string Status, string RowVersion)
    : ICommand<Response<Guid>>;
```

```csharp
// ChangeTicketStatusCommandHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Application.Features.Tickets.Commands.ChangeTicketStatus;

public class ChangeTicketStatusCommandHandler(
    IRepository<Ticket> tickets,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IUserContext userContext,
    IMessageFactory messages)
    : ICommandHandler<ChangeTicketStatusCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(ChangeTicketStatusCommand request, CancellationToken ct)
    {
        var ticket = await tickets.GetTrackedAsync(request.TicketId, ct);
        if (ticket is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        ticket.ChangeStatus(request.Status, userContext.UserId);

        tickets.SetOriginalValue(ticket, nameof(Ticket.RowVersion), Convert.FromBase64String(request.RowVersion));

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsConcurrencyViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.MODIFIED_BY_ANOTHER_USER, MessageType.Conflict);
        }

        return messages.Success(ticket.Id, ApplicationErrors.Ticket.STATUS_CHANGED);
    }
}
```

- [ ] **Step 4: Controller action**

```csharp
// TicketsController.cs
[HttpPost("{id:guid}/status")]
[ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status409Conflict)]
public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeTicketStatusRequest request, CancellationToken ct)
{
    var result = await mediator.Send(new ChangeTicketStatusCommand(id, request.Status, request.RowVersion), ct);
    return this.ToActionResult(result);
}
```

A sub-resource `POST`, not a `PATCH` on the ticket. A status change is a **transition**, not a
field assignment: `PATCH { "status": "Closed" }` invites a client to think it is setting a value,
and the whole design refuses that reading.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC37_ChangeStatus"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ \
        backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "feat(tickets): status transitions (AC-37)"
```

---

### Task 3: Refuse undefined transitions (`AC-38`, `AC-39`)

**Files:**
- Modify: `ChangeTicketStatusCommandHandler.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[Trait("AC", "38")]
[InlineData("New", "Closed")]
[InlineData("Closed", "Resolved")]
[InlineData("New", "Resolved")]
public async Task AC38_ChangeStatus_UndefinedTransition_Returns409NotValidationError(string from, string to)
{
    var ticketId = await CreateTicketInStatusAsync(from);
    var current = await _client.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    var response = await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = to, rowVersion = current!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
[Trait("AC", "39")]
public async Task AC39_ChangeStatus_ToTheStatusAlreadyHeld_Returns409()
{
    var ticketId = await CreateTicketAsync("Self-transition fixture");
    var current = await _client.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    var response = await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = current!.Data!.Status, rowVersion = current.Data.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
[Trait("AC", "30")]
public async Task AC30_ChangeStatus_UnknownStatusValue_Returns400()
{
    var ticketId = await CreateTicketAsync("Bad status value fixture");
    var current = await _client.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    var response = await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = "Escalated", rowVersion = current!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC38_ChangeStatus|FullyQualifiedName~AC39_ChangeStatus|FullyQualifiedName~AC30_ChangeStatus"`
Expected: FAIL — an undefined transition currently throws an unhandled `InvalidOperationException`
(500), not a 409, since nothing yet catches it.

- [ ] **Step 3: Translate the domain's refusal into a 409, and validate the status value into a 400**

```csharp
public async Task<Response<Guid>> Handle(ChangeTicketStatusCommand request, CancellationToken ct)
{
    var ticket = await tickets.GetTrackedAsync(request.TicketId, ct);
    if (ticket is null)
    {
        return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
    }

    var current = TicketStatus.Create(ticket.Status);
    var target = TicketStatus.Create(request.Status); // throws on an unrecognised value — caught
                                                        // by the validation pipeline (AC-30, 400)

    if (current == target)
    {
        return messages.Fail<Guid>(ApplicationErrors.Ticket.ALREADY_IN_STATUS, MessageType.Conflict);
    }

    if (!current.CanTransitionTo(target))
    {
        return messages.Fail<Guid>(ApplicationErrors.Ticket.TRANSITION_NOT_ALLOWED, MessageType.Conflict);
    }

    ticket.ChangeStatus(request.Status, userContext.UserId);
    // ...SetOriginalValue / SaveAsync as in Task 2
}
```

`TicketStatus.Create` throwing `ArgumentException` on an unrecognised value is what `AC-30`'s 400
rides on — the existing `ResponseValidationBehavior`/exception-mapping pipeline already turns an
`ArgumentException` into a 400, the same way every other value object in this codebase does. No
new mapping code needed for that half; only the explicit `current == target` / `CanTransitionTo`
checks above are new, replacing a bare call into `Ticket.ChangeStatus` that let its
`InvalidOperationException` escape unhandled.

**The distinction that is easy to lose**: `Closed` from `New` is a 409 — the request is
well-formed and the state is wrong. `Escalated` is a 400 — there is no such status, so the request
is malformed. Both arrive at the same endpoint and must not answer alike.

- [ ] **Step 4: Register the error codes**

`ApplicationErrors.Ticket` gains `TRANSITION_NOT_ALLOWED` / `ALREADY_IN_STATUS`. `SystemCode`/
`SystemCodeMap`/`ResponseExtensions.MapFailureStatusCode` each get the pairing — the `409` switch
arm needs both. `Resources.yaml` gets ar/en pairs.

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC38_ChangeStatus|FullyQualifiedName~AC39_ChangeStatus|FullyQualifiedName~AC30_ChangeStatus"`
Expected: PASS, 5/5 (3 theory cases + 2 facts).

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandHandler.cs \
        backend/src/CustomerSupport.Application/Errors/ApplicationErrors.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCode.cs \
        backend/src/CustomerSupport.Application/Messages/SystemCodeMap.cs \
        backend/src/CustomerSupport.Api.Shared/Extensions/ResponseExtensions.cs \
        backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml \
        backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "feat(tickets): refuse undefined transitions with 409, not 500 (AC-38, AC-39)"
```

---

### Task 4: Reopen and optimistic concurrency (`AC-40`, `AC-41`)

**Files:**
- Modify: `ChangeTicketStatusCommandHandler.cs` (already carries `SetOriginalValue`/concurrency
  handling from Task 2 — this task is the test claim plus the malformed-`rowVersion` guard)
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
[Trait("AC", "40")]
public async Task AC40_Reopen_PersistsAndRecordsAReopenRow()
{
    var ticketId = await CreateTicketInStatusAsync("Resolved");
    var current = await _client.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    (await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = "Open", rowVersion = current!.Data!.RowVersion }))
        .StatusCode.Should().Be(HttpStatusCode.OK);

    var after = await _client.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
    after!.Data!.History.Should().Contain(h => h.ChangeType == "Reopened");
}

[Fact]
[Trait("AC", "41")]
public async Task AC41_ConcurrentStatusChange_SecondCallerGets409AndFirstChangeSurvives()
{
    var ticketId = await CreateTicketAsync("Concurrency fixture");
    var stale = await _client.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    // First caller wins.
    (await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = "Open", rowVersion = stale!.Data!.RowVersion }))
        .StatusCode.Should().Be(HttpStatusCode.OK);

    // Second caller replays the now-stale rowVersion.
    var response = await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = "Pending", rowVersion = stale.Data.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    var final = await _client.GetFromJsonAsync<Response<TicketRow>>($"/api/Tickets/{ticketId}");
    final!.Data!.Status.Should().Be("Open"); // the first caller's change survives
}

[Fact]
[Trait("AC", "41")]
public async Task AC41_ChangeStatus_WithoutRowVersion_Returns400()
{
    var ticketId = await CreateTicketAsync("Missing rowVersion fixture");

    var response = await _client.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = "Open", rowVersion = "" });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

- [ ] **Step 2: Run tests to verify their current state**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC40_Reopen|FullyQualifiedName~AC41_"`
Expected: `AC40` and the concurrency half of `AC41` PASS immediately — `ChangeStatus` already
appends a `Reopened` row (`TicketChangeType.Reopened` when `current.IsReopenTo(target)`), and Task
2's handler already sets `OriginalValue` from the echoed `rowVersion` before saving, so EF's
concurrency token does the rest. `AC41_ChangeStatus_WithoutRowVersion_Returns400` FAILS — nothing
validates an empty `rowVersion` string yet, and `Convert.FromBase64String("")` throws
`FormatException`, not a documented 400.

- [ ] **Step 3: Validate a malformed `rowVersion` explicitly**

```csharp
// ChangeTicketStatusCommandValidator.cs — new file
using CustomerSupport.Application.Contracts;
using FluentValidation;

namespace CustomerSupport.Application.Features.Tickets.Commands.ChangeTicketStatus;

public class ChangeTicketStatusCommandValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusCommandValidator()
    {
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
```

Picked up automatically by the existing `ResponseValidationBehavior` pipeline — no handler change
needed; a `FormatException` from a malformed-but-non-empty base64 string still needs a guard, added
alongside as a custom rule (`Must(BeValidBase64)`) if the theory case above proves it's reachable.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC40_Reopen|FullyQualifiedName~AC41_"`
Expected: PASS, 3/3.

- [ ] **Step 5: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandValidator.cs \
        backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "test(tickets): reopen and concurrency claims (AC-40, AC-41)"
```

## Definition of done

1. `AC-35`, `AC-37`, `AC-38`, `AC-39`, `AC-40`, `AC-41` each covered by a test naming it.
2. Integration tests against real SQL Server via `CrmApiFactory` — `AC-41` in particular, since
   the in-memory provider does not honour `rowversion`.
3. Suite green, output pasted.
4. **The frontend plan follows once `FEAT-07` and `FEAT-08` are also complete** — `US-128` renders
   all three, and cannot be finished before the actions and history it shows exist.
5. Task records in `EPIC-02-US-016-feat-06-ticket-lifecycle/`.

