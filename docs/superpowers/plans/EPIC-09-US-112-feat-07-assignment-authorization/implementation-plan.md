> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# FEAT-07 — Assignment and per-record authorization · backend plan

> **Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan
> did not precede its implementation.**

**Date:** 2026-08-26
**Feature:** `FEAT-07`, sprint 3, **API-only** · 3 stories · 16 points
**Spec:** `AC-42`…`AC-47`, `BASE-13`
**Decision:** [ADR-0012](../../../adr/0012-seed-agent-and-supervisor-alongside-the-inherited-roles.md) — the role vocabulary
**UI surface:** `US-128`, in [`EPIC-02-US-016-feat-06-ticket-detail-frontend.md`](../EPIC-02-US-016-feat-06-ticket-detail-frontend/implementation-plan.md)

> **The security showcase of the slice.** Endpoint-level authorization cannot satisfy `AC-45` or
> `AC-46` on its own: only the handler has loaded the ticket and can see who holds it.

---

### Task 0: The role vocabulary (`ADR-0012`) — blocks everything

**Files:**
- Modify: `backend/src/CustomerSupport.Infrastructure/Seeders/IdentitySeeder.cs`
- Modify: `backend/src/CustomerSupport.Api.Shared/Extensions/AuthorizationExtensions.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/StaffAdministrationTests.cs`

**Interfaces:**
- Produces: `ApplicationRole.Roles.Agent`/`.Supervisor` (string constants,
  `CustomerSupport.Domain/Entities/Identity/ApplicationRole.cs`), `"Supervisor"`/`"Agent"`
  authorization policies.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task IdentitySeeder_SeedsAgentAndSupervisor()
{
    // Against the seeded database (CrmApiFactory already runs the seeder on startup) —
    // both roles resolve, and a token issued for a user in either role carries it.
    var (agentClient, agentUser) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    var (supervisorClient, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Supervisor);

    agentClient.Dispose();
    supervisorClient.Dispose();
    // No exception on either CreateAuthenticatedClientAsync call is the assertion — the fixture
    // throws if the role doesn't exist to assign.
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~IdentitySeeder_SeedsAgentAndSupervisor"`
Expected: FAIL — `Agent`/`Supervisor` aren't in the seeded role list yet.

- [ ] **Step 3: Seed the two roles and add the policies**

```csharp
// IdentitySeeder.cs — SeedRolesAsync's role list
var roles = new[]
{
    (ApplicationRole.Roles.SuperAdmin, "Super Administrator with full access"),
    (ApplicationRole.Roles.Admin, "Administrator with elevated permissions"),
    (ApplicationRole.Roles.ContentManager, "Manages content on the platform"),
    (ApplicationRole.Roles.StateRepresentative, "State government representative"),
    (ApplicationRole.Roles.User, "Regular user with basic access"),
    (ApplicationRole.Roles.Visitor, "Guest visitor with limited access"),
    (ApplicationRole.Roles.Agent, "Support agent who works assigned tickets"),
    (ApplicationRole.Roles.Supervisor, "Support supervisor who assigns and reassigns tickets"),
};
```

```csharp
// AuthorizationExtensions.cs
// Supervisor is granted wherever Admin is, so an administrator is never locked out of a
// supervisory action. Admin is NOT treated as an Agent: "can administer the platform" and
// "works a support queue" are different claims (ADR-0012), and AC-44 turns on the second.
.AddPolicy("Supervisor", policy => policy.RequireRole("Supervisor", "Admin"))
.AddPolicy("Agent", policy => policy.RequireRole("Agent"))
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~IdentitySeeder_SeedsAgentAndSupervisor"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src/CustomerSupport.Infrastructure/Seeders/IdentitySeeder.cs \
        backend/src/CustomerSupport.Api.Shared/Extensions/AuthorizationExtensions.cs \
        backend/tests/CustomerSupport.Tests/Integration/StaffAdministrationTests.cs
git commit -m "feat(identity): seed Agent/Supervisor roles and policies (ADR-0012)"
```

---

### Task 1: `AssignTicketCommand` — a supervisor assigns work (`AC-42`, `AC-44`)

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommand.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommandHandler.cs`
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

**Interfaces:**
- Consumes: `Ticket.AssignTo(Guid assigneeId, Guid actorId)` (already exists — throws
  `InvalidOperationException` if already assigned to the same user), `IIdentityUserService
  .FindByIdAsync`/`.GetRolesAsync`.
- Produces: `AssignTicketCommand(Guid TicketId, Guid AssigneeId, string RowVersion) :
  ICommand<Response<Guid>>`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "42")]
public async Task AC42_Supervisor_AssignsUnassignedTicket_Returns200()
{
    var (_, agent) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    var ticketId = await CreateTicketAsync("Unassigned fixture");
    var current = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    var response = await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = agent.Id, rowVersion = current!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
[Trait("AC", "44")]
public async Task AC44_Assign_UnknownTargetUser_Returns400KeyedToAssigneeId()
{
    var ticketId = await CreateTicketAsync("Unknown target fixture");
    var current = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    var response = await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = Guid.NewGuid(), rowVersion = current!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var body = await response.Content.ReadFromJsonAsync<Response<object>>();
    body!.Errors.Should().Contain(e => e.Field == "AssigneeId");
}

[Fact]
[Trait("AC", "44")]
public async Task AC44_Assign_TargetIsNotAnAgent_Returns400()
{
    var (_, supervisor2) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Supervisor);
    var ticketId = await CreateTicketAsync("Non-agent target fixture");
    var current = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    var response = await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = supervisor2.Id, rowVersion = current!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC42_Supervisor|FullyQualifiedName~AC44_Assign"`
Expected: FAIL — 404, route doesn't exist.

- [ ] **Step 3: Command + handler**

```csharp
// AssignTicketCommand.cs
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.AssignTicket;

public record AssignTicketCommand(Guid TicketId, Guid AssigneeId, string RowVersion)
    : ICommand<Response<Guid>>;
```

```csharp
// AssignTicketCommandHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Tickets.Commands.AssignTicket;

public class AssignTicketCommandHandler(
    IRepository<Ticket> tickets,
    IIdentityUserService identityUsers,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IUserContext userContext,
    IMessageFactory messages)
    : ICommandHandler<AssignTicketCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(AssignTicketCommand request, CancellationToken ct)
    {
        var ticket = await tickets.GetTrackedAsync(request.TicketId, ct);
        if (ticket is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Ticket.NOT_FOUND);
        }

        var target = await identityUsers.FindByIdAsync(request.AssigneeId, ct);
        if (target is null)
        {
            return FieldFailure(ApplicationErrors.Ticket.ASSIGNEE_NOT_FOUND);
        }

        // AC-44's real enforcement: a supervisor is a real user id, so an existence check alone
        // cannot satisfy this — the target must actually hold the Agent role.
        var roles = await identityUsers.GetRolesAsync(target);
        if (!roles.Contains(ApplicationRole.Roles.Agent))
        {
            return FieldFailure(ApplicationErrors.Ticket.ASSIGNEE_NOT_AN_AGENT);
        }

        // AC-44's full enforcement also rejects a target who exists and is an agent but has since
        // been deactivated — a deactivated agent must not re-enter a queue through assignment.
        // This guard is present in the shipped handler and was the one line the first rewrite of
        // this plan omitted; recorded here so the prose and the real code agree.
        if (!target.IsActive)
        {
            return FieldFailure(ApplicationErrors.Ticket.ASSIGNEE_DEACTIVATED);
        }

        ticket.AssignTo(request.AssigneeId, userContext.UserId);

        tickets.SetOriginalValue(ticket, nameof(Ticket.RowVersion), Convert.FromBase64String(request.RowVersion));

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsConcurrencyViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Ticket.MODIFIED_BY_ANOTHER_USER, MessageType.Conflict);
        }

        return messages.Success(ticket.Id, ApplicationErrors.Ticket.ASSIGNED);
    }

    private Response<Guid> FieldFailure(string code)
    {
        var fieldErrors = new List<FieldError> { new("AssigneeId", SystemCodeMap.Resolve(code), code) };
        return messages.Validation<Guid>(ApplicationErrors.General.VALIDATION_ERROR, fieldErrors);
    }
}
```

`ASSIGNEE_DEACTIVATED` must be registered in all three places the rest of this codebase registers
error codes — `ApplicationErrors.Ticket` (the constant), `SystemCode` + `SystemCodeMap`, and
`Resources.yaml` (an `ar`/`en` pair, or `EveryErrorCode_HasABilingualMessage` fails the build). It
is a **400 field failure** keyed to `AssigneeId`, exactly like `ASSIGNEE_NOT_FOUND`/`ASSIGNEE_NOT_AN_AGENT`.

A **400 keyed to the field**, not 404 — the same rule as `AC-31`: the resource in the URL (the
ticket) exists; the resource named in the body (the assignee) is what's wrong.

- [ ] **Step 4: Controller action**

```csharp
[HttpPost("{id:guid}/assignee")]
[Authorize(Policy = "Supervisor")]
[ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status409Conflict)]
public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketRequest request, CancellationToken ct)
{
    var result = await mediator.Send(new AssignTicketCommand(id, request.AssigneeId, request.RowVersion), ct);
    return this.ToActionResult(result);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC42_Supervisor|FullyQualifiedName~AC44_Assign"`
Expected: PASS, 3/3.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Commands/AssignTicket/ \
        backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "feat(tickets): assignment, target must be a real agent (AC-42, AC-44)"
```

---

### Task 2: An agent cannot assign (`AC-43`)

**Files:**
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
[Trait("AC", "43")]
public async Task AC43_Agent_AssigningAnyTicket_Returns403()
{
    var (agentClient, targetAgent) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    var ticketId = await CreateTicketAsync("Agent cannot assign fixture");
    var current = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    var response = await agentClient.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = targetAgent.Id, rowVersion = current!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    agentClient.Dispose();
}

[Fact]
[Trait("AC", "43")]
public async Task AC43_Agent_AssigningTheirOwnTicket_StillReturns403()
{
    var (agentClient, self) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    var ticketId = await CreateTicketAsync("Own ticket fixture");
    var current = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
    await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = self.Id, rowVersion = current!.Data!.RowVersion });
    var afterAssign = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    // The case a "reasonable" ownership shortcut gets wrong: assignment is a supervisory act
    // regardless of who currently holds the ticket. Permission precedes ownership.
    var response = await agentClient.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = self.Id, rowVersion = afterAssign!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    agentClient.Dispose();
}
```

- [ ] **Step 2: Run to verify current state**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC43_Agent"`
Expected: PASS already — `[Authorize(Policy = "Supervisor")]` on the action (Task 1, Step 4) is
the entire control. Both tests exist to **claim** `AC-43` with a named test, including the
parenthetical case (own-ticket) that a handler-level "do you own this?" shortcut would get wrong —
the policy on the endpoint refuses it before any handler code runs, which is why this story is
separable from `US-120`'s handler-level check.

- [ ] **Step 3: Commit**

```bash
git add backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "test(tickets): claim AC-43, including the own-ticket parenthetical"
```

---

### Task 3: Status change belongs to the assignee (`AC-45`, `AC-46`, `AC-47`)

**Files:**
- Modify: `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommandHandler.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

**Interfaces:**
- Consumes: `Ticket.IsAssignedTo(Guid userId)` (already exists —
  `AssigneeId is not null && AssigneeId == userId`), `IUserContext.HasAnyRole`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
[Trait("AC", "45")]
public async Task AC45_Agent_ChangingAnotherAgentsTicket_Returns403AndTicketUnchanged()
{
    var (_, holder) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    var (otherAgentClient, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    var ticketId = await CreateTicketAsync("Not-mine fixture");
    var current = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
    await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = holder.Id, rowVersion = current!.Data!.RowVersion });
    var afterAssign = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    var response = await otherAgentClient.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = "Open", rowVersion = afterAssign!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    otherAgentClient.Dispose();
}

[Fact]
[Trait("AC", "46")]
public async Task AC46_Agent_ChangingTheirOwnTicket_Returns200()
{
    var (agentClient, holder) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    var ticketId = await CreateTicketAsync("Mine fixture");
    var current = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
    await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = holder.Id, rowVersion = current!.Data!.RowVersion });
    var afterAssign = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    var response = await agentClient.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = "Open", rowVersion = afterAssign!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    agentClient.Dispose();
}

[Fact]
[Trait("AC", "47")]
public async Task AC47_Supervisor_ChangingAnyTicket_Returns200()
{
    var (_, holder) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    var ticketId = await CreateTicketAsync("Supervisor override fixture");
    var current = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");
    await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/assignee",
        new { assigneeId = holder.Id, rowVersion = current!.Data!.RowVersion });
    var afterAssign = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    var response = await _supervisor.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = "Open", rowVersion = afterAssign!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
[Trait("AC", "45")]
public async Task AC45_Agent_ChangingAnUnassignedTicket_Returns403()
{
    var (agentClient, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);
    var ticketId = await CreateTicketAsync("Unassigned, agent tries fixture");
    var current = await _supervisor.GetFromJsonAsync<Response<TicketDetailRow>>($"/api/Tickets/{ticketId}");

    // An unassigned ticket has no assignee, so IsAssignedTo returns false for everyone — an
    // implementation that treated null as "anyone" would hand every agent every unassigned ticket.
    var response = await agentClient.PostAsJsonAsync($"/api/Tickets/{ticketId}/status",
        new { status = "Open", rowVersion = current!.Data!.RowVersion });

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    agentClient.Dispose();
}
```

- [ ] **Step 2: Run tests to verify current state**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AC45_|FullyQualifiedName~AC46_|FullyQualifiedName~AC47_"`
Expected: all 4 PASS already — `ChangeTicketStatusCommandHandler` (rewritten in
`EPIC-02-US-016-feat-06-ticket-lifecycle`'s Task 2/3) already carries:

```csharp
var isSupervisor = userContext.HasAnyRole(ApplicationRole.Roles.Supervisor, ApplicationRole.Roles.Admin);
if (!isSupervisor && !ticket.IsAssignedTo(userContext.UserId))
{
    return messages.Fail<Guid>(ApplicationErrors.Ticket.NOT_ASSIGNED_TO_YOU, MessageType.Forbidden);
}
```

placed before the transition-table check, at the top of `Handle`. No endpoint policy can express
this: `AC-45` and `AC-46` differ only by *which ticket* is addressed, knowable only after the
ticket is loaded — inside the handler, after any policy has already run. These four tests are the
claim; if any failed, this is where the check would be added (same handler, same place).

- [ ] **Step 3: Register `NOT_ASSIGNED_TO_YOU` if not already present, and commit**

Confirm `ApplicationErrors.Ticket.NOT_ASSIGNED_TO_YOU` and its `SystemCode`/`SystemCodeMap`/403
mapping exist (they should, per the handler code above); if any test in Step 2 actually failed,
this is the three-place registration this project's own `FEAT-16` lesson always points back to.

```bash
git add backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "test(tickets): claim per-record status-change authorization (AC-45, AC-46, AC-47)"
```

## Ordering note

`AC-45`'s test needs a ticket assigned to *another* agent, so this task's tests cannot run before
`AssignTicketCommand` (Task 1) works — the fixtures above already reflect that dependency.

## Definition of done

1. `AC-42`…`AC-47` each covered by a test naming it.
2. `US-035` TC-01 and `US-013` TC-02 revisited and closed, or their `partial` status re-justified.
3. Suite green, output pasted.
4. Task records in `EPIC-09-US-112-feat-07-assignment-authorization/`.

