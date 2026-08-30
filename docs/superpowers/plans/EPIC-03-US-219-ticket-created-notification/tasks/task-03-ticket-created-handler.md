# Task 03 — TicketCreatedEventHandler

**Criteria:** `AC-N1`, `AC-N5` (unit level); `AC-N2`/`AC-N3` verified in Task 04.

## Files

- `Application/Features/Tickets/Events/TicketCreatedEventHandler.cs` — new.
- `backend/tests/CustomerSupport.Tests/Unit/Features/Tickets/Events/TicketCreatedEventHandlerTests.cs` — new.

## Steps (TDD — failing unit test first)

1. Failing unit tests over a fake `IIdentityUserService` and a fake `INotificationGateway`:
   - **AC-N1:** given a linked customer user, asserts the gateway `SendAsync` is called once with a
     single in-app channel, `RecipientUserId` = the user id, `TemplateCode == "TICKET_CREATED"`, and
     `Variables["Message"]` contains the ticket reference.
   - **AC-N5:** given `FindByCustomerIdAsync → null`, asserts the gateway is **not** called and no
     exception is thrown.
2. Implement `TicketCreatedEventHandler` exactly as `implementation-plan.md`'s contract fragment:
   resolve the user, return early when unlinked (`AC-N5`), else send the in-app dispatch
   (`DeduplicationKey: ticket-created:{TicketId}` to keep a retried pass single-fire).

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~TicketCreatedEventHandler"`

**Commit:** `feat: notify ticket's customer on ticket creation`

**Deviation log:** The handler test lives under `Unit/Features/Tickets/Events/` (mirroring the
handler's folder) rather than the plan's `Unit/Features/Tickets/`. `FindByCustomerIdAsync` returns
`ApplicationUser?` (not `Guid?`), so the handler checks the user for null and uses its `Id`, and the
AC-N5 empty-customer-id case is exercised by the lookup returning null for `Guid.Empty`.
