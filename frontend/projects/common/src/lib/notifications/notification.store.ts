import { computed, Injectable, signal } from '@angular/core';
import { InAppNotification } from './notification.model';

/**
 * The client-side in-app inbox. Fed by live SignalR pushes (`RealtimeService`) and by hydration
 * from `GET /api/Notifications`. Kept as signals so the bell badge and dropdown update without a
 * manual refresh.
 */
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
