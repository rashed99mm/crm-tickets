# Task 1 — Notification model

**Satisfies:** FN-2, FN-8  
**Files:** `frontend/projects/common/src/lib/notifications/notification.model.ts`

## Steps

```ts
export interface InAppNotification {
  id: string;
  title: string;
  message: string;
  type: string;
  isRead: boolean;
  createdAt: string;
}

export interface InAppPushPayload {
  id: string;
  title: string;
  message: string;
  type: string;
  createdAt: string;
}

export function toInAppNotification(p: InAppPushPayload): InAppNotification {
  return { ...p, isRead: false };
}
```

## Run
`npx ng test common --watch=false`

## Expected
The `common` library type-checks and builds.
