# T4 — Refactor Application command handlers to use IDbExceptionTranslator

**AC:** AC-R2
**Status:** done — all four handlers (`CreateCustomerCommandHandler`, `UpdateCustomerCommandHandler`, `AssignTicketCommandHandler`, `ChangeTicketStatusCommandHandler`) inject `IDbExceptionTranslator` and carry no EF Core import.

## What this task does

Updates 4 command handlers that currently catch `DbUpdateException` or `DbUpdateConcurrencyException` to instead use `IDbExceptionTranslator`:

1. `CreateCustomerCommand.cs` — replace `catch (DbUpdateException ex) when (UniqueViolation.WasHit(ex))` with `translator.IsUniqueViolation(ex)`
2. `UpdateCustomerCommand.cs` — same pattern
3. `AssignTicketCommand.cs` — replace `catch (DbUpdateConcurrencyException)` with `translator.IsConcurrencyViolation(ex)`
4. `ChangeTicketStatusCommand.cs` — same pattern

Removes `using Microsoft.EntityFrameworkCore;` from all four files.

## Files to modify

- `backend/src/CustomerSupport.Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommand.cs`
- `backend/src/CustomerSupport.Application/Features/Customers/Commands/UpdateCustomer/UpdateCustomerCommand.cs`
- `backend/src/CustomerSupport.Application/Features/Tickets/Commands/AssignTicket/AssignTicketCommand.cs`
- `backend/src/CustomerSupport.Application/Features/Tickets/Commands/ChangeTicketStatus/ChangeTicketStatusCommand.cs`

## Verification

`dotnet build` succeeds. All 4 handlers compile without EF Core references.
