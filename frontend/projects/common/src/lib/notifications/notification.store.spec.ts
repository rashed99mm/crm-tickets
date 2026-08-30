import { NotificationStore } from './notification.store';

describe('NotificationStore', () => {
  it('prepends live notifications and computes unread count', () => {
    const store = new NotificationStore();
    const first = {
      id: 'n-1',
      title: 'First',
      message: 'One',
      type: 'TicketAssigned',
      isRead: false,
      createdAt: '2026-08-27T10:00:00Z',
    };
    const second = { ...first, id: 'n-2', title: 'Second' };

    store.add(first);
    store.add(second);

    expect(store.items().map((item) => item.id)).toEqual(['n-2', 'n-1']);
    expect(store.unreadCount()).toBe(2);
  });

  it('marks one notification read and clears the inbox', () => {
    const store = new NotificationStore();
    store.setAll([
      { id: 'n-1', title: 'One', message: 'One', type: 'Test', isRead: false, createdAt: '' },
      { id: 'n-2', title: 'Two', message: 'Two', type: 'Test', isRead: false, createdAt: '' },
    ]);

    store.markRead('n-1');

    expect(store.items()[0].isRead).toBe(true);
    expect(store.items()[1].isRead).toBe(false);
    expect(store.unreadCount()).toBe(1);

    store.clear();
    expect(store.items()).toEqual([]);
    expect(store.unreadCount()).toBe(0);
  });
});
