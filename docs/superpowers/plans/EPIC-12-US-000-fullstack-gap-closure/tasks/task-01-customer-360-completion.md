# Task 01 - Customer 360 Completion

**Status:** Ready  
**Spec:** `docs/superpowers/specs/EPIC-12-US-000-fullstack-gap-closure-sdd.md`  
**Closes gaps:** WhatsApp, tags, plan/tier, email verified, manager, MRR, timezone, HQ, customer tickets lane, note edit/delete, attachment rename.

## Files

- Backend domain: `Customer.cs`, `CustomerNote.cs`, `CustomerAttachment.cs`
- Backend application: `Features/Customers/**`, `Features/Tickets/Queries/GetTickets`
- Backend API: `CustomersController.cs`, `TicketsController.cs`
- Frontend API: `common/src/lib/customers/customer.api.ts`, `common/src/lib/tickets/ticket.api.ts`
- Frontend UI: `admin-app/src/app/features/customers/*`
- Tests: customer domain/handler/controller tests, Angular customer detail tests

## Implementation

- Extend customer profile fields and migration.
- Add tag persistence/search.
- Add `customerId` filter to ticket queue.
- Add customer note update/delete commands.
- Add attachment rename command.
- Update Customer 360 Stitch panel with real fields and editable dialogs.

## Code Example

```csharp
public sealed record UpdateCustomerProfileCommand(
    Guid Id,
    string? WhatsAppNumber,
    string? PlanTier,
    Guid? AccountManagerId,
    decimal? MonthlyRecurringRevenue,
    string? TimeZone,
    string? Headquarters,
    IReadOnlyList<string> Tags) : IRequest<Response<CustomerDto>>;
```

```ts
updateProfile(id: string, request: UpdateCustomerProfileRequest): Observable<Customer> {
  return this.http.put<Customer>(`/api/Customers/${id}/profile`, request);
}
```

## Acceptance

- [ ] Customer profile fields load from API.
- [ ] Saving profile persists all fields and tags.
- [ ] Customer tickets lane calls ticket queue with `customerId`.
- [ ] Notes can be edited/deleted with authorization.
- [ ] Attachments can be renamed without binary reupload.
- [ ] Stitch customer detail visual review passes desktop/mobile.

## Evidence

Pending.
