import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { NotificationDto, PaginatedList } from './notification.model';

/**
 * Client for the existing backend notifications API (`NotificationsController`).
 * The envelope interceptor unwraps the `Response<T>` wrapper, so these return the typed data
 * directly — same pattern as `AuthApi`.
 */
@Injectable({ providedIn: 'root' })
export class NotificationApi {
  private readonly http = inject(HttpClient);

  list(page = 1, pageSize = 50): Observable<PaginatedList<NotificationDto>> {
    return this.http.get<PaginatedList<NotificationDto>>('/api/Notifications', {
      params: { page, pageSize },
    });
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`/api/Notifications/${id}/read`, {});
  }

  unreadCount(): Observable<number> {
    return this.http.get<number>('/api/Notifications/unread/count');
  }
}
