# T8 — Fix frontend PagedResult and remove duplicate page interfaces

**AC:** AC-R5
**Status:** done — `PagedResult<T>` declares `pageIndex`; `ticket.api.ts`, `customer.api.ts`, `staff.api.ts` all return `PagedResult<T>` directly, no duplicate page interfaces remain.

## What this task does

1. Fixes `PagedResult<T>` in `api-response.ts` to declare `pageIndex` instead of `page`
2. Replaces the 5 duplicate local interfaces (`TicketPage`, `CustomerPage`, `CustomerNotePage`, `CustomerAttachmentPage`, `StaffUserList`) with `PagedResult<T>` from the canonical definition
3. Updates all consumers of these local interfaces to use `PagedResult<TItem>`
4. Removes the now-unnecessary duplicate interface definitions and their documenting comments

## Files to modify

- `frontend/projects/common/src/lib/api/api-response.ts` — fix `page` → `pageIndex`
- `frontend/projects/common/src/lib/tickets/ticket.api.ts` — remove `TicketPage`, use `PagedResult<TicketListItem>`
- `frontend/projects/common/src/lib/customers/customer.api.ts` — remove `CustomerPage`, `CustomerNotePage`, `CustomerAttachmentPage`, use `PagedResult<T>`
- `frontend/projects/common/src/lib/auth/staff.api.ts` — remove `StaffUserList`, use `PagedResult<StaffUser>`
- All consumer files that reference the removed interfaces

## Verification

`cd frontend && npx ng build admin-app` succeeds.
