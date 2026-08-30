# Frontend UI/UX Gap Closure Implementation Plan

**Spec:** [`../../specs/EPIC-13-US-311-ui-gap-closure-sdd.md`](../../specs/EPIC-13-US-311-ui-gap-closure-sdd.md)  
**Status:** Active  
**Layer:** Frontend with SDD traceability updates  

## Objective

Close the gap-report items that can be implemented against existing Angular services and backend
contracts, and explicitly route the remaining gaps to their owning SDD stories.

## Tasks

| Task | File | Scope |
|---|---|---|
| 01 | [`tasks/task-01-live-chat-ai-branding-admin.md`](tasks/task-01-live-chat-ai-branding-admin.md) | Chat session, AI route/button, branding settings, KB category/version/insights, export, user actions, forgot password |

## Implementation Notes

- Do not create UI that implies backend support where no endpoint exists.
- Prefer existing services: `ChatStore`, `ChatApi`, `BrandingApi`, `BrandingStore`,
  `PlatformSettingApi`, `KbAdminApi`, `AuditLogApi`, and `StaffApi`.
- Use direct CSV export for current-page audit/user export because no report-export endpoint is
  needed for the visible table data.
- Record blocked gaps in the spec rather than leaving placeholders in the screen.

## Verification

Targeted commands for this slice:

```text
cd frontend
npx ng test admin-app --watch=false
npx ng build admin-app
npx ng build portal-app
```

Record observed output in this plan after execution.
