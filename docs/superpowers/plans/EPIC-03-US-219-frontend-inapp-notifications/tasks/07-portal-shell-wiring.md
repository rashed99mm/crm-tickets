# Task 6 — Portal shell wiring + proxy

**Satisfies:** FN-1, FN-2, FN-3, FN-4, FN-7  
**Files:** `frontend/projects/portal-app/src/app/layout/shell.component.ts`,
`frontend/projects/portal-app/src/app/layout/shell.component.html`,
`frontend/proxy.portal.conf.json`

## Steps

1. Same wiring as Task 5 for the portal shell: inject `NotificationStore`, `NotificationApi`,
   `RealtimeService`; hydrate via `notificationApi.list()`; add the bell + dropdown; `markRead(id)`
   calls the API then the store.

2. `frontend/proxy.portal.conf.json` must route both `/api` and `/hubs` to the external host:

   ```json
   {
     "/api": { "target": "http://localhost:5095", "secure": false, "changeOrigin": true },
     "/hubs": { "target": "http://localhost:5095", "secure": false, "changeOrigin": true, "ws": true }
   }
   ```

## Run
`npx ng test portal-app --watch=false --include=**/shell.component.spec.ts`

## Expected
The portal bell works against 5095 and shows live + hydrated notifications.
