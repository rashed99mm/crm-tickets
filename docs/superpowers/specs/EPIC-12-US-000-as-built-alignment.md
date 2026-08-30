# EPIC-12-US-000: As-Built Alignment Specification

## Scope

This specification records the final cross-cutting contracts implemented across the staff CRM and
customer portal. It complements the detailed story acceptance criteria and does not replace them.

## Contracts

| Concern | Contract |
|---|---|
| Staff API | `InternalApi` is a .NET 10 web API protected by staff authorization policies |
| Portal API | `ExternalApi` is a .NET 10 web API for customer and anonymous portal flows |
| Staff chat | `Agent`, `Supervisor`, and `Admin` may access `/api/chat/*` |
| Portal chat | Customers use `/api/external/chat/*` with an opaque session token |
| Session lifecycle | Waiting -> Active -> Closed; Claim is not offered for Active sessions |
| Portal ticket detail | History, attachments, reply, survey, and a Live Chat link remain available in one view |
| Notifications | Ticket creation and status changes notify relevant recipients without duplicates |

## Acceptance Checks

- A regular portal token cannot call the internal staff chat controller.
- A support-role token can load the staff chat queue.
- A Waiting session can be claimed.
- An Active session opens directly and does not issue a second claim request.
- A portal ticket detail page links to `/live-chat` rather than `/api/chat`.
- Admin and portal development builds complete successfully.

