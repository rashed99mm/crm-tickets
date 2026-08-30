# FEAT-24..27 Communication Channels Frontend Implementation Plan

**Spec:** `docs/superpowers/specs/EPIC-10-US-203-communication-channels-frontend.md`  
**Backend spec:** `docs/superpowers/specs/EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md`  
**Status:** Active; live-chat queue/session has an implementation slice, email/WhatsApp/SMS inboxes remain pending backend-contract review.

## Execution Boundary

The backend channel contracts must be implemented and stable before frontend implementation begins.
The frontend plan is intentionally written now so the next vertical slice has an executable contract;
it does not claim that any endpoint, hub, component, or test already exists.

## Existing Code To Preserve

- `common/src/lib/tickets/ticket.api.ts:72-99` — message types and record request.
- `admin-app/src/app/features/tickets/ticket-messages.component.ts:48-95` — signal state and loading pattern.
- `admin-app/src/app/features/tickets/ticket-messages.component.html:104-129` — oldest-first timeline markup.
- `common/src/lib/realtime/realtime.service.ts:44-110` — authenticated SignalR lifecycle and event registration.
- `admin-app/src/app/app.config.ts:13-30` — `/hubs/main` and interceptor providers.
- `admin-app/src/app/layout/shell.component.ts:53-65` — single navigation table.
- `portal-app/src/app/app.routes.ts:13-32` — public routes outside authenticated `/app`.
- `common/src/lib/api/api-response.ts:19-34` — field-error and envelope contract.
- `common/src/lib/notifications/notification.api.ts:6-27` — typed HTTP client style.
- `common/src/lib/testing/no-hardcoded-strings.spec.ts` — translation enforcement.

## Contract Checklist

Before coding each task, compare the backend controller DTO and route with the typed frontend client.
Do not invent final URLs or event payload names. The planned concepts are:

| Surface | Host/authentication | Frontend consumer |
|---|---|---|
| Waiting/claim queue | InternalApi/authenticated | `ChatApi`, admin queue |
| Agent transcript | InternalApi + `/hubs/main` | `ChatApi`, `RealtimeService` |
| Customer start/messages | ExternalApi/session token | `ChatApi`, anonymous widget |
| Web form | ExternalApi/anonymous | `WebFormApi`, public form |
| Ticket messages | InternalApi/authenticated | existing `TicketApi` and timeline |

## Sequence

1. Task 07 widens the existing message contract and removes channel-specific template branches.
2. Task 08 adds the authenticated waiting queue and route.
3. Task 09 adds the authenticated session transcript and SignalR event handling. The 2026-08-29
   gap-closure slice wires `ChatSessionComponent` to `ChatStore` and removes the static transcript.
4. Task 10 adds the anonymous customer live-chat surface after the backend session contract is stable.
5. Task 11 adds the anonymous web-form surface after the backend submission contract is stable.
6. Task 12 runs the frontend evidence gate and updates records from actual output.

Each task is one commit and follows failing test → implementation → focused test → review.

## Evidence Gate

Run only after the backend contracts compile:

```text
cd frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
```

Record actual output, update the task records and README, then update the delivery-plan status. A
frontend task is not complete because its component exists; its named test and build evidence must
also exist.
