# Task 01 — Message Timeline

**Story/AC:** US-202, original AC-3.4 (AC1, AC2), spec AC-202.1/AC-202.2
**Layer:** Frontend component and integration tests
**Status:** done

## Executable checklist

- [ ] Inspect `ticket-messages.component.ts/html`, `ticket-detail.component.*`, `TicketApi`, and
  `AsyncState`; confirm the existing implementation is the intended source of truth.
- [ ] First add failing TestBed tests in
  `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.spec.ts` named
  `US202_MessageTimeline_RendersOldestFirstWithDirectionChannelSenderBodyAndTime`,
  `US202_MessageTimeline_RendersDistinctEmptyState`,
  `US202_MessageTimeline_RendersLoadFailureInsteadOfEmptyState`, and
  `US202_MessageTimeline_UsesTicketApiListMessages`.
- [ ] Use a stub `TicketApi`, three messages with distinct `sentAt` values, an empty array, and a
  rejected Observable. Assert rendered DOM order and `time[datetime]`.
- [ ] Add `US202_TicketDetail_RendersMessageTimelineForLoadedTicket` to the parent spec if its child
  wiring is not covered.
- [ ] Run `cd frontend; npx ng test admin-app --watch=false --include "**/ticket-messages.component.spec.ts"`.
- [ ] Fix only production defects found by the failing tests; preserve error-versus-empty semantics,
  sender provenance, escaped body rendering, and no client-side reorder.
- [ ] Run `npx ng test admin-app --watch=false` and `npx ng build admin-app`; paste actual output in
  story evidence and update this task's status.

## Exact files

- New/modify: `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.spec.ts`.
- Possible production edits: `ticket-messages.component.ts`, `ticket-messages.component.html`,
  `ticket-detail.component.spec.ts`, `ticket-detail.component.ts/html`.
- Contract inspection only: `frontend/projects/common/src/lib/tickets/ticket.api.ts` and common UI/i18n files.

## Verification commands

```powershell
cd frontend
npx ng test admin-app --watch=false --include "**/ticket-messages.component.spec.ts"
npx ng test admin-app --watch=false
npx ng build admin-app
```

## Status evidence

**Status: done** — executed 2026-08-27. US-202 story moved from `partial` to `done`.

Tests added in `ticket-messages.component.spec.ts` (stub `TicketApi`, three messages with distinct
`sentAt`, an empty array, and a `throwError` Observable):

- `US202_MessageTimeline_RendersOldestFirstWithDirectionChannelSenderBodyAndTime`
- `US202_MessageTimeline_RendersDistinctEmptyState`
- `US202_MessageTimeline_RendersLoadFailureInsteadOfEmptyState`
- `US202_MessageTimeline_UsesTicketApiListMessages`

Plus `US202_TicketDetail_RendersMessageTimelineForLoadedTicket` in `ticket-detail.component.spec.ts`
(real HTTP, real parent→child wiring).

Red→green: the channel/order test failed in the first run against the existing template — it rendered
direction, sender, body and time but not per-message channel, violating AC-202.1. Fixed
`ticket-messages.component.html` with a localized channel badge. No other regression.

Actual output:
- `npx ng test admin-app --watch=false --include "**/ticket-messages.component.spec.ts"` → **4 passed (1 file)**.
- `npx ng test admin-app --watch=false --include "**/ticket-detail.component.spec.ts"` → **10 passed (1 file)**.
- `npx ng build admin-app` → clean, 0 errors.
- `npx ng test admin-app --watch=false` → **139/140**. Single failure pre-existing and unrelated:
  `nav-routes.spec.ts` reports `/reports/sla-performance` and `/reports/agent-performance` are not
  offered by the sidebar (reporting feature US-6xx gap). NOT caused by this task.

E2E `TicketDetail_MessageTimelineFlow` (TC-04) remains unimplemented — out of scope for this task.

## Deviation record

`None yet.` Record any server ordering or localization discrepancy instead of compensating silently in
the component.
