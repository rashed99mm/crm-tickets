# Task 08 — Live-Chat Waiting Queue

**Criteria:** `FB-2` / `CC-25`  
**Status:** pending  
**Commit:** none

## Context

The admin route table uses lazy components at `admin-app/src/app/app.routes.ts:23-39`, and
`NAV_ITEMS` is the single navigation source at `admin-app/src/app/layout/shell.component.ts:53-65`.
The existing `US-607` live queue is a ticket/reporting queue and is not a live-chat session queue.

## Files

- Create `common/src/lib/channels/chat.model.ts` and `chat.api.ts`.
- Create `admin-app/src/app/features/chat/chat-queue.component.ts/html/spec.ts`.
- Modify `admin-app/src/app/app.routes.ts` and `layout/shell.component.ts`.
- Modify `common/src/public-api.ts` and translations.

## Steps

1. Confirm the implemented backend route and DTO names from `GetWaitingSessionsQuery` before writing
   the client; do not copy a URL from this plan if the backend contract changed.
2. Write HttpTestingController tests for the waiting-session GET and claim POST.
3. Implement `ChatApi` following `NotificationApi`'s injected `HttpClient` pattern.
4. Build the queue with the existing `AsyncState` states used by `ticket-messages.component.ts:57-95`.
5. Display session status, customer display data, and wait time calculated from `createdAt`.
6. On successful claim, navigate to `/chat/sessions/:id`; retain the error state on failure.
7. Add the route and nav item together. Existing nav tests require every nav item to resolve.

## Run

```text
cd frontend
npx ng test common --watch=false --include="**/chat.api.spec.ts"
npx ng test admin-app --watch=false --include="**/chat-queue.component.spec.ts"
npx ng test admin-app --watch=false --include="**/app.routes.spec.ts"
```
