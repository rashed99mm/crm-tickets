# task-F1 — `LiveChatRealtimeService` (anonymous hub client, common)

**Status:** Complete
**AC:** FB-4, FB-5, FB-8

## Change
- New `frontend/projects/common/src/lib/channels/live-chat-realtime.service.ts`: a small
  `@microsoft/signalr` client that connects to `/hubs/chat?token=<sessionToken>` (via the pure
  `liveChatHubUrl()` helper) with `withAutomaticReconnect()`, listens for `ChatMessageReceived`, and
  exposes `incoming` + `state` signals. Explicitly NOT the shared `RealtimeService` (the
  authenticated `/hubs/main` client — non-reusable per
  `communication-channels-frontend-design.md:105`). `connect()` swallows transport/build failures so
  `state` surfaces disconnects instead of throwing.
- New spec `live-chat-realtime.service.spec.ts` (FB-8 token-only URL; safe disconnect; disconnected
  when unreachable).
- Exported from `common/src/public-api.ts`.

## Evidence (real output from `npx ng test common --watch=false`)
```
Test Files  48 passed (48)
     Tests  205 passed (205)
```
