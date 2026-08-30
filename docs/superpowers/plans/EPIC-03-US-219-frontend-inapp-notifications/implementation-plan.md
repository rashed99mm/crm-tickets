# FEAT-15 Frontend In-App Notifications over SignalR — Implementation Plan

**Spec:** `docs/superpowers/specs/EPIC-13-US-311-frontend-inapp-notifications-design.md`  
**Epic:** `EPIC-03 Communication Channels` / `EPIC-09 Security & Administration`  
**Sprint:** `9`  
**Status:** in progress — backend T0 (hub auto-subscribe) + frontend T1–T7 implemented; unit tests added.

## Existing code to preserve

- `common/src/lib/auth/session.store.ts` (`token()`, `userId()`, `isAuthenticated()`).
- `common/src/lib/auth/auth.api.ts` (envelope-unwrap pattern).
- `admin-app/src/app/layout/shell.component.ts` (+ `.html`, `.spec.ts`).
- `portal-app/src/app/layout/shell.component.ts` (+ `.html`, `.spec.ts`).
- `backend/.../Hubs/MainHub.cs`, `backend/.../Notifications/RealTimeNotifier.cs` (already pushes to `user:{userId}`).
- `frontend/proxy.conf.json` (admin→5074), `frontend/proxy.portal.conf.json` (portal→5095).

## Contract

```ts
// notifications/notification.model.ts
export interface InAppNotification {
  id: string; title: string; message: string;
  type: string; isRead: boolean; createdAt: string;
}
export interface InAppPushPayload {
  id: string; title: string; message: string; type: string; createdAt: string;
}
export function toInAppNotification(p: InAppPushPayload): InAppNotification {
  return { ...p, isRead: false };
}

// notifications/notification.api.ts (sketch)
export interface NotificationDto {
  id: string; title: string; message: string;
  notificationType: string; isRead: boolean; createdAt: string;
}
export interface PaginatedList<T> { items: T[]; totalCount: number; pageIndex: number; pageSize: number; }
```

## Tasks

### Task 0 — Backend: hub auto-subscribe (security; FN-1/FN-6)
**Files:** `CustomerSupport.Api.Shared/Hubs/MainHub.cs`  
**Steps:**
1. In `OnConnectedAsync`, resolve `Context.UserIdentifier` and `await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{id}")`.
2. In `OnDisconnectedAsync`, `await Groups.RemoveFromGroupAsync(Context.ConnectionId, ...)`.
3. The client no longer calls `JoinGroup` for the in-app flow.  
**Run:** `dotnet build backend/CustomerSupport.slnx`  
**Expected:** Build clean; in-app pushes reach only the owning connection.  
**Commit:** `fix: auto-subscribe SignalR connections to their user group`

### Task 1 — Notification model
**Files:** `common/src/lib/notifications/notification.model.ts`  
**Steps:** add `InAppNotification`, `InAppPushPayload`, `toInAppNotification`.  
**Run:** `npx ng test common --watch=false` (lib compiles)  
**Expected:** type-checks.  
**Commit:** `feat: add in-app notification model`

### Task 2 — Notification store
**Files:** `common/src/lib/notifications/notification.store.ts`  
**Steps:** `NotificationStore` (root) with `items` signal, `unreadCount` computed, `add/markRead/setAll/clear`.  
**Run:** `npx ng test common --watch=false --include=**/notification.store.spec.ts`  
**Expected:** `unreadCount` tracks `isRead`; `add` prepends; `markRead` flips one.  
**Commit:** `feat: add client notification store`

### Task 3 — Notification API
**Files:** `common/src/lib/notifications/notification.api.ts`  
**Steps:** `NotificationApi` (root) with `list()`, `markRead(id)`, `unreadCount()` over existing endpoints.  
**Run:** `npx ng test common --watch=false --include=**/notification.api.spec.ts` (HttpTestingController)  
**Expected:** GET/POST to correct URLs; envelope unwrapped.  
**Commit:** `feat: add notification API client`

### Task 4 — Realtime service
**Files:** `common/src/lib/realtime/realtime.service.ts`  
**Steps:** `RealtimeService` (root) building `HubConnection` with `accessTokenFactory: () => session.token()`, `withAutomaticReconnect`, `on('NotificationReceived')` → `zone.run(() => store.add(...))`, `effect` on `isAuthenticated` to start/stop.  
**Run:** `npx ng test common --watch=false --include=**/realtime.service.spec.ts`  
**Expected:** on authenticated, `start()` called; `NotificationReceived` → `store.add`; token read live.  
**Commit:** `feat: add SignalR realtime notification service`

### Task 5 — Admin shell wiring + proxy
**Files:** `admin-app/src/app/layout/shell.component.ts`, `.html`; `frontend/proxy.conf.json`  
**Steps:** inject `NotificationStore` + `NotificationApi` + `RealtimeService`; hydrate on init; bell `CsIcon` with `unreadCount()` badge; dropdown lists `items()`; click → `api.markRead(id)` then `store.markRead(id)`; proxy gains `"/hubs"` with `ws:true` → 5074.  
**Run:** `npx ng test admin-app --watch=false --include=**/shell.component.spec.ts`  
**Expected:** badge shows count; clicking marks read.  
**Commit:** `feat: wire admin in-app notification bell over SignalR`

### Task 6 — Portal shell wiring + proxy
**Files:** `portal-app/src/app/layout/shell.component.ts`, `.html`; `frontend/proxy.portal.conf.json`  
**Steps:** same as Task 5 for portal; `proxy.portal.conf.json` gets `/api`→5095 and `"/hubs"` (ws:true)→5095.  
**Run:** `npx ng test portal-app --watch=false --include=**/shell.component.spec.ts`  
**Expected:** portal bell works against 5095.  
**Commit:** `feat: wire portal in-app notification bell over SignalR`

### Task 7 — Evidence gate
**Steps:**
1. `npx ng build admin-app` and `npx ng build portal-app` with warnings-as-errors.
2. Run common + both app unit suites.
3. Manual: start InternalApi + ExternalApi, log in to each app, trigger an in-app dispatch, confirm the
   bell updates live and on reload.
4. Update `plans/INDEX.md` and story status from observed output only.
