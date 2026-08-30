# Task 07 — Message Timeline Channel Rendering

**Criteria:** `FB-1` / `CC-24`  
**Status:** pending  
**Commit:** none

## Context

`MESSAGE_CHANNELS` currently contains only `System` and `Email` at
`frontend/projects/common/src/lib/tickets/ticket.api.ts:76-79`. The timeline template has separate
System/Email conditionals at `ticket-messages.component.html:34-39` and `:112-114`. Those branches
will mislabel every new channel as Email.

## Files

- Modify `frontend/projects/common/src/lib/tickets/ticket.api.ts`.
- Create `frontend/projects/common/src/lib/channels/channel-labels.ts`.
- Modify `frontend/projects/common/src/public-api.ts`.
- Modify `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.html`.
- Modify `frontend/projects/common/src/lib/i18n/translations.ts`.
- Modify `ticket.api.spec.ts` and `ticket-messages.component.spec.ts`.

## Steps

1. Add failing tests for all six channel values and for a WhatsApp timeline message.
2. Widen the shared channel union to exactly the backend allow-list.
3. Add one channel-to-translation-key lookup. Both the select and message chip must use it; do not
   add a new `if`/ternary for each provider.
4. Add English and Arabic labels for WhatsApp, SMS, WebForm, and LiveChat.
5. Preserve the direction icon at template line 110, oldest-first ordering, `fieldError()`, and the
   existing empty/error/loading states.
6. Verify that the record form can send `WhatsApp` and `SMS` channel values for backend reply dispatch.

## Run

```text
cd frontend
npx ng test common --watch=false --include="**/ticket.api.spec.ts"
npx ng test admin-app --watch=false --include="**/ticket-messages.component.spec.ts"
```

## Expected Evidence

Tests prove every supported channel receives its own label and no new channel is treated as Email.
