# Task 4 — Realtime service

**Satisfies:** FN-1, FN-2, FN-5, FN-6  
**Files:** `frontend/projects/common/src/lib/realtime/realtime.service.ts`

## Steps

```ts
import { effect, Injectable, inject, NgZone, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { SessionStore } from '../auth/session.store';
import { NotificationStore } from '../notifications/notification.store';
import { InAppPushPayload, toInAppNotification } from '../notifications/notification.model';

@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly session = inject(SessionStore);
  private readonly store = inject(NotificationStore);
  private readonly zone = inject(NgZone);
  private connection?: HubConnection;

  constructor() {
    // Start when authenticated, stop (and clear) on sign-out.
    effect(() => (this.session.isAuthenticated() ? this.ensureStarted() : this.stop()));
  }

  private ensureStarted(): void {
    if (this.connection?.state === HubConnectionState.Connected) {
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/main', { accessTokenFactory: () => this.session.token() ?? '' })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    this.connection.on('NotificationReceived', (p: InAppPushPayload) =>
      this.zone.run(() => this.store.add(toInAppNotification(p))),
    );

    void this.connection.start().catch(() => {
      /* automatic reconnect will retry */
    });
  }

  private stop(): void {
    void this.connection?.stop();
    this.connection = undefined;
    this.store.clear();
  }
}
```

## Run
`npx ng test common --watch=false --include=**/realtime.service.spec.ts`

## Expected
When `session.isAuthenticated()` is true, `start()` is called; a `NotificationReceived` message is added
to the store; the access token is read live from `session.token()`.
