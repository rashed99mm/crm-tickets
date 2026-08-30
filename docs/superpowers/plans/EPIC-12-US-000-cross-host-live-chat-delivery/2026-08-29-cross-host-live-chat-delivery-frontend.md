# Frontend Plan — Cross-Host Live-Chat Delivery (portal receive path)

**Specs:** `EPIC-10-US-203-communication-channels-frontend.md` (FB-4/FB-5/FB-8/FB-9),
`EPIC-03-US-201-communication-channels-whatsapp-livechat-webforms.md` (CC-14/CC-16/CC-28/CC-29),
`EPIC-12-US-000-cross-host-live-chat-delivery-addendum.md` (CC-30..CC-34)
**Feature:** `FEAT-26` live chat
**Date:** 2026-08-29

## Grounding facts (cited)

- The portal widget today is **REST-only**: it starts an anonymous session, sends messages, and
  appends the returned `ChatMessageDto`. It never connects to `/hubs/chat`, so an **agent reply never
  surfaces in real time**.
  - `frontend/projects/portal-app/src/app/features/live-chat/live-chat-widget.component.ts:27-37`
    (signals; `sessionToken`, `messages`), `:69-84` (`startChat` stores the token + appends the
    initial message), `:101-110` (`send` appends the server-returned message).
- The shared `RealtimeService` is the **authenticated `/hubs/main`** client — it attaches a bearer
  token (`realtime.service.ts:68`), listens for `NotificationReceived`
  (`realtime.service.ts:76`), and clears the inbox on stop. Per
  `communication-channels-frontend-design.md:105` it must not be reused: "small anonymous SignalR
  client for `/hubs/chat?token=...`; do not loosen `/hubs/main`."
- The apps configure realtime via `REALTIME_CONFIG` with `hubUrl: REALTIME_HUB_PATH` (`/hubs/main`)
  in both `app.config.ts:16`. The anonymous hub is the **same origin**, different path:
  `/hubs/chat?token=<sessionToken>` (ExternalApi host).
- The dev proxy already forwards `/hubs` (with `ws: true`) to `http://localhost:5095`:
  `frontend/proxy.portal.conf.json:7-12`. No proxy change needed.
- Backend push payload `ChatMessagePushPayload(Id, SessionId, SenderType, SenderName, SenderId,
  Body, SentAt)` is serialized for the `ChatMessageReceived` method; its field names match the
  existing client `ChatMessageDto` (`chat.model.ts:15-23`). The `RealTimeNotifier` pushes to the
  `chat:{sessionId}` group (backend `RealTimeNotifier.cs:35-39`), so the client must filter to its
  own session (FB-3/CC-31).

## Acceptance criteria this plan satisfies

- **FB-4 / CC-14**: after a successful start, connect to the session-scoped `/hubs/chat` with the
  opaque token.
- **FB-5 / CC-16**: render the other party's messages that arrive via SignalR, and expose the
  connecting / connected / reconnecting / disconnected states to the UI.
- **FB-8 / CC-28/CC-29**: only the opaque token travels on the wire — no customer/ticket id.
- **FB-9**: all new strings are translated (en/ar) using the existing `t`.
- **CC-30/CC-31/CC-34** (backend): the frontend consumes the pump-delivered `ChatMessageReceived`.

## Design

Add a dedicated, small **anonymous live-chat realtime client** in `common` (not touching
`RealtimeService`). It owns its own `HubConnection`, connects to `/hubs/chat?token=...` with
`withAutomaticReconnect()`, exposes an incoming-message signal + a connection-state signal, and is
visible only to the portal widget. The widget drives it after a start, appends arrivals to
`messages` (deduped by `id`), and disconnects on end chat / teardown.

## Tasks (frontend; TDD — test first, then implement)

### Task F1 — `LiveChatRealtimeService` (common)
Files: `frontend/projects/common/src/lib/channels/live-chat-realtime.service.ts` (new),
`.../live-chat-realtime.service.spec.ts` (new), `frontend/projects/common/src/public-api.ts` (edit),
`REALTIME_HUB_PATH`+`REALTIME_CONFIG` already in `realtime.config.ts`).
- `@Injectable({ providedIn: 'root' })`; ctor uses `REALTIME_CONFIG` to derive the origin (falling
  back to relative `/hubs/chat` when `hubUrl` is relative) — actually simpler: always connect to a
  relative `/hubs/chat` path (same origin as the SPA, rewritten by the proxy). Listen for
  `ChatMessageReceived`; convert the payload to `ChatMessageDto`; expose
  `readonly incoming = signal<ChatMessageDto | null>(null)` and `readonly state = signal<...>`.
  `connect(sessionToken)`, `disconnect()`.
- Do not reuse `RealtimeService`; no auth token; query-string token only (FB-8).
- Export from `public-api.ts` beside the chat exports at `:36-40`.
AC: FB-4, FB-5, FB-8. Tests: connect URL carries `?token=` with the opaque token; unknown-token
refusal surfaces `disconnected`; two session tokens → events filtered to the connected session.

### Task F2 — widget live receive + states
Files: `portal-app/src/app/features/live-chat/live-chat-widget.component.ts`/`.html` (edit),
`live-chat-widget.component.spec.ts` (edit), portal translations file.
- After `startAnonymousSession` success, call `realtime.connect(res.sessionToken)` and subscribe to
  `incoming`: append `msg` if `msg.sessionId === this.sessionId()` and not already present
  (mirror `ChatStore.appendMessage` at `chat.store.ts:54-59`).
- Track the realtime `state` so the header shows connecting / connected / reconnecting / offline
  (FB-5). `endChat()`/destroy call `realtime.disconnect()`.
- All new UI strings via `t` in en + ar (FB-9).
AC: FB-4, FB-5, FB-8, FB-9. Tests: with the realtime service faked, an inbound `ChatMessageDto` for
the active session renders in the list once (dedupe); a message for another session is ignored; the
state label changes with the connection state.

### Task F3 — verify + build
- `cd frontend && npx ng test common --watch=false` and `npx ng test portal-app --watch=false`
  (paste output), `npx ng build portal-app --configuration development`.
- Live two-app check (backend already restarted): open the portal widget, send a message, and
  confirm an agent message (driven from the internal host) appears without a reload.

## Order
F1 → F2 → F3. Each task is one commit with a failing test first.

## Shipped check
Both test suites green (real output pasted), build clean, widget renders agent messages pushed
across the host boundary, plan task records + README updated.
