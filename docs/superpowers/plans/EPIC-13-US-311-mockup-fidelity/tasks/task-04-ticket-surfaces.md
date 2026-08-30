# Task 04 · Adapt ticket surfaces

**Criteria:** `AC-407`, `AC-408`, `AC-409`, `AC-416`, `AC-417`, `AC-418`  
**Status:** Completed (All ticket queue, detail, create and AI panel specs passed)

## Changes

Adapt `ticket_queue`, `submit_ticket`, `ticket_detail_chatbot`, `ai_ticket_management_workspace`
and `ai_powered_agent_workspace` into the existing ticket components. Keep API models, pagination,
typed forms, message timeline, SLA countdown and AI service boundaries unchanged. Render missing AI
or integration data in the designed location as non-interactive unavailable states.

## Test-first cases

- `AC407_QueueUsesReferenceTableCompositionAndStates`
- `AC408_CreateTicketMatchesFormCompositionAndValidation`
- `AC409_DetailRendersTimelineMetadataAndAiRegions`
- `AC416_TicketScreensDistinguishLoadingEmptyAndError`
- `AC417_MissingTicketCapabilitiesRenderUnavailableWithoutControls`
- `AC418_TicketFormsAndActionsAreKeyboardAccessible`

## Done when

Queue, create and detail flows keep their existing behaviour while their layout is reference-faithful
and usable at all four viewport tiers.

## Exact files

- Queue: `frontend/projects/admin-app/src/app/features/tickets/ticket-queue.component.ts`,
  `.html`, `.spec.ts`.
- Create: `ticket-create.component.ts`, `.html`, `.spec.ts` in the same directory.
- Detail: `ticket-detail.component.ts`, `.html`, `.spec.ts` and
  `ticket-messages.component.ts`, `.html`, `.spec.ts`.
- AI region: `ai-panel.component.ts`, `.html`, `.spec.ts`.
- Contracts to preserve: `frontend/projects/common/src/lib/tickets/ticket.api.ts`,
  `sla-countdown.component.*`, `ai/ai.api.ts`.
- Reference files: `stitch_smart_support_ticketing_crm/{ticket_queue,submit_ticket,ticket_detail_chatbot,ai_ticket_management_workspace,ai_powered_agent_workspace}/code.html`.

## Live implementation example

`ticket-queue.component.ts` already models `loading`, `empty`, `loaded` and `error` through
`AsyncState` (lines 59–117). Keep that state model while replacing the current result markup with
the reference table. Use `ticket.escalationState` for the escalation badge and `CsStatusPill` for
status; do not derive a new status from CSS classes or swallow errors with `of([])`.

## Execution commands

```text
cd frontend
npx ng test admin-app --watch=false --include='**/features/tickets/**/*.spec.ts'
npx ng test common --watch=false --include='**/tickets/*.spec.ts'
npx ng build admin-app
```
