# Task 3 — Notification API

**Satisfies:** FN-3, FN-4  
**Files:** `frontend/projects/common/src/lib/notifications/notification.api.ts`

## Steps

```ts
import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { NotificationDto } from './notification.model';

export interface PaginatedList<T> {
  items: T[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
}

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
```

## Run
`npx ng test common --watch=false --include=**/notification.api.spec.ts` (HttpTestingController)

## Expected
`list()` GETs `/api/Notifications`, `markRead(id)` POSTs `/api/Notifications/{id}/read`; envelope unwrapped.
