# Task 13 - Ticket Lifecycle Completion

**Status:** In progress  
**Closes gaps:** lifecycle metadata visibility, escalation owner handoff, stale row-version recovery, transition clarity.

## Files

- Backend: `TicketsController.cs`, `TakeEscalationCommandHandler.cs`, `TicketDtos.cs`
- Frontend API: `common/src/lib/tickets/ticket.api.ts`
- Frontend UI: `admin-app/src/app/features/tickets/ticket-detail.component.*`

## Implementation

- Surface lifecycle timestamps already returned by the backend DTO.
- Show assignee display names instead of placeholder/guid-only text.
- Add escalation-owner action for escalated tickets.
- Echo rowVersion through status, assignment, and escalation-owner mutations.
- Preserve 409 recovery by re-reading the ticket after mutation refusal.

## Acceptance

- [x] Detail header shows assignee name when present.
- [x] Detail header shows first response, last response, resolved, closed, and escalation owner metadata when present.
- [x] Supervisor can take ownership of escalated tickets from the detail screen.
- [x] Escalation owner mutation posts `assigneeId` and `rowVersion`.
- [x] Invalid escalation-owner domain transition returns the transition-not-allowed code instead of a success-domain code.

## Evidence

- Added `TicketApi.takeEscalation`.
- Extended `TicketDetail` with `assigneeName`, lifecycle timestamps, and escalation owner fields from the backend DTO.
- Added `takeEscalation()` UI action and `data-testid="escalation-owner-action"` select.
- Updated backend `TakeEscalationCommandHandler` invalid-state catch to return `TICKET_TRANSITION_NOT_ALLOWED`.
- Verified `npx ng test admin-app --watch=false --include=projects/admin-app/src/app/features/tickets/ticket-detail.component.spec.ts` passed 15 tests, including AC506 escalation ownership.
- Verified `npx ng build admin-app` passed with existing dashboard unused-import warnings and initial bundle budget warning.
