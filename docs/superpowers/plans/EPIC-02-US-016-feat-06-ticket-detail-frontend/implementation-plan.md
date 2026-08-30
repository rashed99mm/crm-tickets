> Rewritten 2026-08-27 to add real code; the feature described here shipped earlier � this plan did not precede its implementation.

# FEAT-06 — Ticket detail with guarded actions · frontend plan

> **Rewritten 2026-08-27 to add real code; the feature described here shipped earlier — this plan
> did not precede its implementation.**

**Date:** 2026-08-26
**Feature:** `FEAT-06`, sprint 3, **vertical** · frontend half — and the screen that closes
`FEAT-07`'s and `FEAT-08`'s user surface as well
**Spec:** `AC-61`
**Backend counterparts:** [`FEAT-06`](../EPIC-02-US-016-feat-06-ticket-lifecycle/implementation-plan.md) ·
[`FEAT-07`](../EPIC-09-US-112-feat-07-assignment-authorization/implementation-plan.md) ·
[`FEAT-08`](../EPIC-02-US-016-feat-08-ticket-history/implementation-plan.md) — all three complete

`US-128` lists nine backend stories in its **Ships with** row. It is sequenced last in the sprint
because it renders all of them, and one frontend story closes three features' UI.

---

### Task 0: A backend gap this plan uncovered — the assignable-agents endpoint

**Files:**
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetAssignableAgents/GetAssignableAgentsQuery.cs`
- Create: `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetAssignableAgents/GetAssignableAgentsQueryHandler.cs`
- Modify: `backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs`
- Test: `backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

The assign control needs a list of agents to pick from, and there was **no endpoint that could
supply one**. `/api/Users` is `[Authorize(Policy = "Admin")]`, and a supervisor is not an
administrator (ADR-0012).

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("AC", "42")]
public async Task AssignableAgents_ReturnsOnlyUsersInTheAgentRole()
{
    var response = await _supervisor.GetAsync("/api/Tickets/assignable-agents");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<Response<List<AgentRow>>>();
    body!.Data.Should().OnlyContain(a => !string.IsNullOrEmpty(a.Name));
}

[Fact]
[Trait("AC", "43")]
public async Task AssignableAgents_AgentIsRefused()
{
    var (agentClient, _) = await _factory.CreateAuthenticatedClientAsync(ApplicationRole.Roles.Agent);

    var response = await agentClient.GetAsync("/api/Tickets/assignable-agents");

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    agentClient.Dispose();
}

public sealed record AgentRow(Guid Id, string Name, string Email);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AssignableAgents"`
Expected: FAIL — 404, route doesn't exist.

- [ ] **Step 3: Query + handler + narrow DTO**

```csharp
// GetAssignableAgentsQuery.cs
using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetAssignableAgents;

public record AssignableAgentDto(Guid Id, string Name, string Email);

public record GetAssignableAgentsQuery : IQuery<Response<IReadOnlyList<AssignableAgentDto>>>;
```

```csharp
// GetAssignableAgentsQueryHandler.cs
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetAssignableAgents;

public class GetAssignableAgentsQueryHandler(IIdentityUserService identityUsers)
    : IQueryHandler<GetAssignableAgentsQuery, Response<IReadOnlyList<AssignableAgentDto>>>
{
    public async Task<Response<IReadOnlyList<AssignableAgentDto>>> Handle(GetAssignableAgentsQuery request, CancellationToken ct)
    {
        var agents = await identityUsers.GetUsersInRoleAsync(ApplicationRole.Roles.Agent, ct);

        IReadOnlyList<AssignableAgentDto> options =
            [.. agents.Select(a => new AssignableAgentDto(a.Id, a.FullName, a.Email ?? string.Empty))];

        return Response<IReadOnlyList<AssignableAgentDto>>.Ok(options, SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
```

Narrow by design: exposes exactly the two fields the picker needs, rather than widening the
user-administration surface to supervisors.

- [ ] **Step 4: Controller action**

```csharp
[HttpGet("assignable-agents")]
[Authorize(Policy = "Supervisor")]
[ProducesResponseType(typeof(Response<IReadOnlyList<AssignableAgentDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public async Task<IActionResult> GetAssignableAgents(CancellationToken ct)
{
    var result = await mediator.Send(new GetAssignableAgentsQuery(), ct);
    return this.ToActionResult(result);
}
```

Placed **before** `{id:guid}/status` in the controller's route order, or ASP.NET's routing would
never reach it — `assignable-agents` would try to bind as a `Guid` id first.

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~AssignableAgents"`
Expected: PASS, 2/2.

- [ ] **Step 6: Commit**

```bash
git add backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetAssignableAgents/ \
        backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs \
        backend/tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs
git commit -m "feat(tickets): assignable-agents endpoint the frontend picker needs (AC-42, AC-44)"
```

---

### Task 1: `TicketApi` methods (`get`, `changeStatus`, `assign`, `listAssignableAgents`)

**Files:**
- Modify: `frontend/projects/common/src/lib/tickets/ticket.api.ts`
- Test: `frontend/projects/common/src/lib/tickets/ticket.api.spec.ts`

- [ ] **Step 1: Write the failing test**

```ts
it('TicketApi_ChangeStatus_PostsStatusAndRowVersion', () => {
  const api = TestBed.inject(TicketApi);
  const http = TestBed.inject(HttpTestingController);

  api.changeStatus('t-1', 'Open', 'AAAA').subscribe();

  const request = http.expectOne('/api/Tickets/t-1/status');
  expect(request.request.method).toBe('POST');
  expect(request.request.body).toEqual({ status: 'Open', rowVersion: 'AAAA' });
  request.flush({ success: true, code: 'CON030', message: 'OK', data: null, errors: [] });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test common --watch=false --include='**/ticket.api.spec.ts'`
Expected: FAIL if `changeStatus` doesn't exist yet; otherwise this is a coverage claim like the
backend's Task-1-of-FEAT-06 pattern.

- [ ] **Step 3: Methods (already present in `TicketApi`, shown for completeness)**

```ts
get(id: string): Observable<TicketDetail> {
  return this.http.get<TicketDetail>(`/api/Tickets/${id}`);
}

changeStatus(id: string, status: TicketStatus, rowVersion: string): Observable<unknown> {
  return this.http.post(`/api/Tickets/${id}/status`, { status, rowVersion });
}

assign(id: string, assigneeId: string, rowVersion: string): Observable<unknown> {
  return this.http.post(`/api/Tickets/${id}/assignee`, { assigneeId, rowVersion });
}

listAssignableAgents(): Observable<readonly AssignableAgent[]> {
  return this.http.get<readonly AssignableAgent[]>('/api/Tickets/assignable-agents');
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx ng test common --watch=false --include='**/ticket.api.spec.ts'`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/projects/common/src/lib/tickets/ticket.api.ts frontend/projects/common/src/lib/tickets/ticket.api.spec.ts
git commit -m "feat(tickets): TicketApi status/assign/assignable-agents methods"
```

---

### Task 2: `TicketDetailComponent` — render, guard, and recover from conflict (`AC-61`)

**Files:**
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts`
- Modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html`
- Test: `frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.spec.ts`

**Interfaces:**
- Consumes: `PERMITTED_TRANSITIONS: Record<TicketStatus, readonly TicketStatus[]>` (a client-side
  copy of the server's own transition table, `common/tickets/ticket.api.ts`), `SessionStore.hasRole`.

- [ ] **Step 1: Write the failing tests**

```ts
it('AC61: renders customer summary, history and the status action', () => {
  const fixture = render('t-1');
  flushDetail(fixture, { ...BASE_TICKET, status: 'Open' });

  const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
  expect(text).toContain(BASE_TICKET.customer.name);
  expect(fixture.nativeElement.querySelector('[data-testid="history-timeline"]')).not.toBeNull();
});

it('AC61+AC37: status action offers only the transitions permitted from the current status', () => {
  const fixture = render('t-1');
  flushDetail(fixture, { ...BASE_TICKET, status: 'Open' }); // Open -> Pending | Resolved only

  const options = Array.from(
    fixture.nativeElement.querySelectorAll('#detail-status option'),
  ).map((o: Element) => (o as HTMLOptionElement).value);

  expect(options).not.toContain('Closed');
});

it('AC61: assign action is hidden for an agent', () => {
  // sessionStore stubbed to hasRole('Supervisor') === false, hasRole('Admin') === false
  const fixture = render('t-1');
  flushDetail(fixture, BASE_TICKET);

  expect(fixture.nativeElement.querySelector('[data-testid="assign-action"]')).toBeNull();
});

it('AC61+AC41: status change echoes the rowVersion it read', () => {
  const fixture = render('t-1');
  flushDetail(fixture, { ...BASE_TICKET, rowVersion: 'RV1' });

  fixture.componentInstance.changeStatus('Pending');

  const request = http.expectOne('/api/Tickets/t-1/status');
  expect(request.request.body).toEqual({ status: 'Pending', rowVersion: 'RV1' });
});

it('AC61+AC41: a 409 shows the message and re-reads the ticket', () => {
  const fixture = render('t-1');
  flushDetail(fixture, BASE_TICKET);
  fixture.componentInstance.changeStatus('Pending');
  http.expectOne('/api/Tickets/t-1/status').flush(
    { success: false, code: 'ERR024', message: 'Modified by another user', data: null, errors: [] },
    { status: 409, statusText: 'Conflict' },
  );

  // Component re-reads on failure (see Step 3) — a second GET fires.
  const reread = http.expectOne('/api/Tickets/t-1');
  reread.flush({ success: true, code: 'CON035', message: 'OK', data: BASE_TICKET, errors: [] });
  fixture.detectChanges();

  expect((fixture.nativeElement as HTMLElement).textContent).toContain('Modified by another user');
});
```

(`render`, `flushDetail`, `http`, `BASE_TICKET` follow this project's established
`TestBed`/`HttpTestingController` fixture pattern — see `ticket-queue.component.spec.ts` for the
exact scaffolding shape to mirror.)

- [ ] **Step 2: Run tests to verify current state**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/ticket-detail.component.spec.ts'`
Expected: the render/history/hidden-assign/echoed-rowVersion cases PASS already — the shipped
component already implements `availableTransitions` (`computed` over `PERMITTED_TRANSITIONS[current
.status]`), `canAssign` (`session.hasRole('Supervisor') || session.hasRole('Admin')`), and
`changeStatus`/`assign` both calling through `run()`, which echoes `current.rowVersion`. The
409-re-reads case is what to verify carefully — confirm `run()`'s error branch actually re-fetches
before treating this task as closed.

- [ ] **Step 3: If the 409 case doesn't already re-read, this is the fix**

```ts
private run(work: { subscribe(observer: { next: () => void; error: (e: unknown) => void }): unknown }): void {
  this.busy.set(true);
  this.actionError.set(null);

  work.subscribe({
    next: () => {
      this.busy.set(false);
      this.load();
    },
    error: (error: unknown) => {
      this.busy.set(false);
      this.actionError.set(this.toApiError(error));
      this.reloadPreservingError(); // stale rowVersion by definition on a 409 — re-read, don't patch
    },
  });
}
```

On a 409 the client's `rowVersion` is stale by definition. Patching the local copy would leave the
form holding a version the server has already superseded, and the next attempt would fail the same
way. Re-reading is the only honest recovery; the server's message already says why.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd frontend && npx ng test admin-app --watch=false --include='**/ticket-detail.component.spec.ts'`
Expected: PASS, all 5.

- [ ] **Step 5: Commit**

```bash
git add frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.ts \
        frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.html \
        frontend/projects/admin-app/src/app/features/tickets/ticket-detail.component.spec.ts
git commit -m "test(tickets): claim AC-61 across render, guard and conflict-recovery cases"
```

## The transition table exists twice now, and that is a decision

Task 2's `availableTransitions` puts the permitted-transition list in the client so the action
offers `Open → Pending` rather than all five statuses. The server holds the same table in
`TicketStatus`, and **the server remains the authority** — the client copy is a courtesy that
stops the UI offering a move it knows will be refused. A drifted client is not a security hole,
only a worse experience: an offered-but-forbidden transition still gets a 409, which Task 2's own
conflict-recovery case renders.

## Definition of done

1. `AC-61` covered by component tests naming it, including the hidden-assign case.
2. `HttpTestingController` assertions on method, URL and body for all three mutations.
3. `npx ng test admin-app --watch=false` and `npx ng test common --watch=false` green, **output
   pasted**; `npx ng build admin-app` clean.
4. Story files' status updated from what was executed.
5. Task records in `EPIC-02-US-016-feat-06-ticket-detail-frontend/`.

`US-128` TC-04 (the Playwright pass) belongs to `AC-64` / the end-to-end journey, which is a
separate plan. It is **not** claimed here.

