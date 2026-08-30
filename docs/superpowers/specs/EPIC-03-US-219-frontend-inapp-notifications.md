# Frontend In-App Notifications over SignalR

**Epic:** `EPIC-03 Communication Channels` / `EPIC-09 Security & Administration`  
**Sprint:** `9 — Notification Gateway and Communication Channels`  
**Feature:** `FEAT-15`  
**Related:** backend `EPIC-03-US-219-notification-gateway.md` (NG-1…NG-10), `NotificationsController` (existing), frontend `SessionStore`, `AuthApi`.

## Problem

The backend can deliver an in-app notification by persisting a `Notification` row and pushing it over
SignalR to the group `user:{userId}` (`RealTimeNotifier` → `IHubContext<MainHub>`). The frontend has no
SignalR client, no in-app notification store, and no UI — the admin shell only has a commented
"notification bell". Staff (and portal customers) therefore never see in-app notifications.

## Assumptions

- **A1:** `@microsoft/signalr` `^10.0.11` is already installed in the workspace; no new dependency.
- **A2:** `SessionStore` (`common`) is the source of truth for `token()` and `userId()`; the JWT from
  `accessTokenFactory` is accepted by the hub's `Authenticated` policy.
- **A3:** The hub **server-side** auto-subscribes each connection to `user:{Context.UserIdentifier}` on
  connect (backend T0). The client never calls `JoinGroup`, so one user cannot subscribe to another's group.
- **A4:** `Context.UserIdentifier` equals the backend `userId` (the JWT `sub`/NameIdentifier), so the
  auto-subscribed group matches the group `RealTimeNotifier` targets.
- **A5:** The existing envelope interceptor unwraps HTTP responses, so `NotificationApi` returns typed
  data directly (as `AuthApi` does).
- **A6:** Both apps authenticate: admin against InternalApi (5074), portal customers against ExternalApi
  (5095). The hub's `Authenticated` policy is satisfiable for both.
- **A7:** Dev proxies currently only forward `/api`; `/hubs` must be added with `ws:true` per app.

## Out of scope

- Toast/alert pop-ups (only the bell + dropdown inbox for now).
- Push notifications / service workers.
- Server-sent in-app notifications for anonymous portal browsing (requires a user).

## Acceptance criteria

- **FN-1:** Given an authenticated user, when the app loads, then the client opens a SignalR connection
  to `/hubs/main` using the current access token and receives pushes for `user:{userId}` only.
- **FN-2:** Given a backend in-app dispatch for the user, when the `NotificationReceived` message
  arrives, then the notification appears in the bell dropdown and the unread count increments, without a
  page refresh.
- **FN-3:** Given the bell dropdown is open, when the user clicks a notification, then it is marked read
  via `POST /api/Notifications/{id}/read`, the store reflects `isRead`, and the unread count decrements.
- **FN-4:** Given the user reloads the page, when the shell initialises, then existing notifications are
  hydrated from `GET /api/Notifications` so the inbox is not empty.
- **FN-5:** Given the user signs out, when the session clears, then the SignalR connection stops and the
  store clears.
- **FN-6:** Given the connection drops, when SignalR auto-reconnects, then the user is re-subscribed
  (server-side) and subsequent pushes arrive.
- **FN-7:** Given an authenticated user on **either** admin or portal, when an in-app notification is
  dispatched, then it appears in that app's bell (admin→5074, portal→5095).
- **FN-8:** Given any notification payload, then no secret, OTP, or token value is rendered in the UI or
  logged client-side.

## Design

`common` owns the reusable pieces; each app wires its shell:

- `notifications/notification.model.ts` — `InAppNotification` + `toInAppNotification(payload)`.
- `notifications/notification.store.ts` — `NotificationStore` (root): `items` signal, `unreadCount`
  computed, `add/markRead/setAll/clear`.
- `notifications/notification.api.ts` — `NotificationApi` (root): `list()`, `markRead(id)`,
  `unreadCount()` over existing endpoints.
- `realtime/realtime.service.ts` — `RealtimeService` (root): builds the `HubConnection`, registers
  `NotificationReceived`, and reacts to `session.isAuthenticated()` via `effect` (start/stop); callbacks
  run in `NgZone`.
- Both `shell.component` (admin + portal): inject store + api, hydrate on init, render bell + dropdown,
  mark read.

Security is enforced by backend T0 (server auto-subscribe); the client never chooses a group name.

## API and error contract

- HTTP uses the existing `Response<T>` envelope (unwrapped by interceptor). `NotificationApi.list()`
  returns the unwrapped `PaginatedList<NotificationDto>`; `markRead()` returns `void`.
- SignalR carries only `{id,title,message,type,createdAt}` — never credentials/OTP.
