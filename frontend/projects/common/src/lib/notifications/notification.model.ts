export interface InAppNotification {
  id: string;
  title: string;
  message: string;
  type: string;
  isRead: boolean;
  createdAt: string;
}

/** What the backend pushes over SignalR (`InAppPushPayload`). */
export interface InAppPushPayload {
  id: string;
  title: string;
  message: string;
  type: string;
  createdAt: string;
}

/** What `GET /api/Notifications` returns per item. */
export interface NotificationDto {
  id: string;
  userId: string;
  title: string;
  message: string;
  notificationType: string;
  channel: string;
  status: string;
  readAt: string | null;
  sentAt: string | null;
  retryCount: number;
  createdAt: string;
}

export interface PaginatedList<T> {
  items: T[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
}

/** Maps a live SignalR push into the client store (always unread on arrival). */
export function toInAppNotification(p: InAppPushPayload): InAppNotification {
  return { id: p.id, title: p.title, message: p.message, type: p.type, isRead: false, createdAt: p.createdAt };
}

/** Maps a hydrated backend DTO into the client store. */
export function notificationFromDto(d: NotificationDto): InAppNotification {
  return {
    id: d.id,
    title: d.title,
    message: d.message,
    type: d.notificationType,
    isRead: d.readAt !== null,
    createdAt: d.createdAt,
  };
}
