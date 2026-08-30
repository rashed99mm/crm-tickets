# Task 10 — Anonymous Customer Live-Chat Widget

**Criteria:** `FB-4`, `FB-5` / `CC-14`, `CC-16`  
**Status:** pending  

## Context

Portal public routes are outside the authenticated `/app` group (`portal-app/src/app/app.routes.ts:13-32`).
The existing `portal-app/features/chat/chat.component.ts` is the FEAT-21 AI assistant and must remain
unchanged. The anonymous chat hub is deliberately separate from authenticated `/hubs/main`.

## Files

- Extend `common/src/lib/channels/chat.model.ts` and `chat.api.ts`.
- Create an anonymous chat SignalR client under `common/src/lib/channels/`.
- Create `portal-app/src/app/features/live-chat/live-chat-widget.component.ts/html/spec.ts`.
- Modify `portal-app/src/app/app.routes.ts` and translations.

## Steps

1. Confirm the backend start/session/message routes and hub event name.
2. Write tests proving session start stores only the opaque token and does not send customer/ticket ids.
3. Implement the widget outside the authenticated route group.
4. Connect to `/hubs/chat` with the session token as specified by the backend; do not weaken `/hubs/main`.
5. Render waiting, active, closed, abandoned, connecting, and reconnecting states.
6. Send and receive messages through the session-scoped contract, rejecting messages for another session.
7. Ensure reload can recover the current session without exposing the token in visible UI or logs.

## Run

```text
cd frontend
npx ng test common --watch=false --include="**/anonymous-chat*.spec.ts"
npx ng test portal-app --watch=false --include="**/live-chat-widget.component.spec.ts"
```
