# Task 2 — Notification store

**Satisfies:** FN-2, FN-3, FN-4, FN-5  
**Files:** `frontend/projects/common/src/lib/notifications/notification.store.ts`

## Steps

```ts
import { computed, Injectable, signal } from '@angular/core';
import { InAppNotification } from './notification.model';

@Injectable({ providedIn: 'root' })
export class NotificationStore {
  private readonly _items = signal<InAppNotification[]>([]);

  readonly items = this._items.asReadonly();
  readonly unreadCount = computed(() => this._items().filter((n) => !n.isRead).length);

  add(n: InAppNotification): void {
    this._items.update((list) => [n, ...list]);
  }

  setAll(list: InAppNotification[]): void {
    this._items.set(list);
  }

  markRead(id: string): void {
    this._items.update((list) => list.map((n) => (n.id === id ? { ...n, isRead: true } : n)));
  }

  clear(): void {
    this._items.set([]);
  }
}
```

## Run
`npx ng test common --watch=false --include=**/notification.store.spec.ts`

## Expected
`unreadCount` tracks `isRead`; `add` prepends; `markRead` flips exactly one entry.
