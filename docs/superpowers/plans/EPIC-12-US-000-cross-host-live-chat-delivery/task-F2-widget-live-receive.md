# task-F2 — widget live receive + connection states

**Status:** Complete
**AC:** FB-4, FB-5, FB-8, FB-9

## Change
- `portal-app/src/app/features/live-chat/live-chat-widget.component.ts`:
  - after `startAnonymousSession` success, `void this.realtime.connect(res.sessionToken)` (FB-4);
  - `effect` watches `this.realtime.incoming()` and appends when `msg.sessionId === this.sessionId()`
    and the id is not already present (`appendMessage`, mirrors `ChatStore.appendMessage`);
  - `connectionState = this.realtime.state` drives the header state label (FB-5); `ngOnDestroy()` and
    `endChat()` call `realtime.disconnect()`.
- `live-chat-widget.component.html`: header now renders connecting / connected / reconnecting /
  offline via `@switch` on `connectionState()` (FB-5).
- `common/src/lib/i18n/translations.ts`: added `chat.session.offline` en `Disconnected` / ar
  `غير متصل` (FB-9).
- Spec in `live-chat-widget.component.spec.ts` fakes `LiveChatRealtimeService` (Vitest `vi.fn()` +
  signals) and adds tests: connect called with opaque token (FB-4), appends scoped push + ignores
  other-session push (FB-5), disconnects on end (FB-4).

## Evidence (real output from `npx ng test portal-app --watch=false`)
```
 Test Files  14 passed (14)
      Tests  65 passed (65)
```
