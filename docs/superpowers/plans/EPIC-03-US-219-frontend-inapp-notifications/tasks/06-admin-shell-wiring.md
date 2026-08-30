# Task 5 — Admin shell wiring + proxy

**Satisfies:** FN-1, FN-2, FN-3, FN-4, FN-7  
**Files:** `frontend/projects/admin-app/src/app/layout/shell.component.ts`,
`frontend/projects/admin-app/src/app/layout/shell.component.html`,
`frontend/proxy.conf.json`

## Steps

1. In `shell.component.ts`, inject the store, api and (to force instantiation) the realtime service:

   ```ts
   private readonly notifications = inject(NotificationStore);
   private readonly notificationApi = inject(NotificationApi);
   private readonly realtime = inject(RealtimeService); // forces the connection effect to run

   constructor() {
     // hydrate the inbox so a reload is not empty (FN-4)
     this.notificationApi
       .list(1, 50)
       .subscribe((page) => this.notifications.setAll(page.items.map(toInAppNotification)));
     // ...existing effect for the document title...
   }

   markRead(id: string): void {
     this.notificationApi.markRead(id).subscribe(() => this.notifications.markRead(id));
   }
   ```

   Expose `notifications` (readonly) to the template.

2. In `shell.component.html`, add the bell with a badge and a dropdown (uses existing `CsIcon`):

   ```html
   <button type="button" class="notification-bell" aria-label="Notifications">
     <cs-icon name="notifications" />
     @if (notifications.unreadCount() > 0) {
       <span class="badge">{{ notifications.unreadCount() }}</span>
     }
   </button>
   <div class="notification-panel">
     @for (n of notifications.items(); track n.id) {
       <button type="button" (click)="markRead(n.id)">
         <strong>{{ n.title }}</strong>
         <span>{{ n.message }}</span>
       </button>
     }
   </div>
   ```

3. `frontend/proxy.conf.json` gains the hub route:

   ```json
   {
     "/api": { "target": "http://localhost:5074", "secure": false, "changeOrigin": true },
     "/hubs": { "target": "http://localhost:5074", "secure": false, "changeOrigin": true, "ws": true }
   }
   ```

## Run
`npx ng test admin-app --watch=false --include=**/shell.component.spec.ts`

## Expected
The bell badge shows `unreadCount()`; clicking an item marks it read (API + store).
