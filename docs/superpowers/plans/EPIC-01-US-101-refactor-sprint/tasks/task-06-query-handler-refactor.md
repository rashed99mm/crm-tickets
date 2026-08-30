# T6 — Refactor Application query handlers to use new repository methods

**AC:** AC-R4
**Status:** done — all five handlers (`GetTicketsQuery`, `GetTicketByIdQuery`, `GetCategoriesQuery`, `GetCustomerAttachmentsQuery`, `GetCustomerNotesQuery`) call the new repository methods; no EF Core import in any of them.

## What this task does

Updates 5 query handlers that currently call `.Query().CountAsync()`, `.Query().ToListAsync()`, `.Query().FirstOrDefaultAsync()`, or complex LINQ chains on `IQueryable<T>`:

1. `GetTicketsQuery.cs` — replace `tickets.Query(filter)` + `.CountAsync()` + LINQ join with repository methods
2. `GetTicketByIdQuery.cs` — replace `tickets.Query().FirstOrDefaultAsync()` and `history.Query().OrderByDescending().ToListAsync()`
3. `GetCategoriesQuery.cs` — replace `categories.Query().OrderBy().Select().ToListAsync()`
4. `GetCustomerAttachmentsQuery.cs` — replace `attachments.Query()` + join + `.CountAsync()` + `.ToListAsync()`
5. `GetCustomerNotesQuery.cs` — replace `notes.Query()` + `.CountAsync()` + `.OrderByDescending().Skip().Take().Select().ToListAsync()`

All `using Microsoft.EntityFrameworkCore;` imports removed from these files.

## Files to modify

- `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTickets/GetTicketsQuery.cs`
- `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetTicketById/GetTicketByIdQuery.cs`
- `backend/src/CustomerSupport.Application/Features/Tickets/Queries/GetCategories/GetCategoriesQuery.cs`
- `backend/src/CustomerSupport.Application/Features/Customers/Queries/GetCustomerAttachments/GetCustomerAttachmentsQuery.cs`
- `backend/src/CustomerSupport.Application/Features/Customers/Queries/GetCustomerNotes/GetCustomerNotesQuery.cs`

## Verification

`dotnet build` succeeds. All handlers compile without EF Core references.
