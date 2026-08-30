# Task 09 — Agent Live-Chat Transcript

**Criteria:** `FB-3` / `CC-26`  
**Status:** pending  
**Commit:** none

## Context

`RealtimeService.on()` stores handlers and registers them on the authenticated `/hubs/main` connection
(`common/src/lib/realtime/realtime.service.ts:107-110`). The admin provider is configured in
`admin-app/src/app/app.config.ts:13-30`. The ticket detail component demonstrates route-input loading
and isolated async state at `ticket-detail.component.ts:62-75` and `:134-153`.

## Files

- Create `common/src/lib/channels/chat.store.ts` and its tests.
- Extend `common/src/lib/channels/chat.api.ts` and `chat.model.ts`.
- Create `admin-app/src/app/features/chat/chat-session.component.ts/html/spec.ts`.
- Modify admin routes and `common/src/public-api.ts`.

## Steps

1. Confirm the backend event name and payload from `ChatHub`/agent message implementation.
2. Write tests for HTTP transcript hydration, send failure, pushed-message append, wrong-session
   filtering, and handler cleanup.
3. Implement a signal-based store that appends only messages belonging to the active session.
4. Register one handler through `RealtimeService`; do not create a second authenticated SignalR
   connection and do not poll.
5. Hydrate the transcript on route load, render oldest-first, and show connection/reconnecting state.
6. Send through the typed API and let the server push be the authoritative append path.
7. Remove the session handler when the component is destroyed.

## Run

```text
cd frontend
npx ng test common --watch=false --include="**/chat.store.spec.ts"
npx ng test admin-app --watch=false --include="**/chat-session.component.spec.ts"
```
