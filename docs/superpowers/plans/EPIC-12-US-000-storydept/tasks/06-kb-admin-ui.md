# Task 06 — KB admin UI (US-509–512)

## Traceability
Epic:   docs/requirements/epics/EPIC-06-knowledge-base.md
Stories: US-509-kb-admin-list.md, US-510-kb-admin-create.md, US-511-kb-admin-edit.md,
         US-512-kb-admin-publish.md (+ frontend half of US-503 for the category dropdown)
FEAT:   FEAT-18 — delivery-plan.md row 11
Spec:   docs/superpowers/specs/EPIC-06-US-504-knowledge-base.md
Plan:   docs/superpowers/plans/EPIC-06-US-504-feat-11-knowledge-base/

## Work
1. `common/src/lib/admin/kb-admin.api.ts` mirroring `admin/permission.api.ts`:
   list(status?,page,pageSize) → GET /api/Contents; get; create → POST /api/Contents;
   update → PUT /api/Contents/{id}; publish/archive → POST /api/Contents/{id}/publish|archive;
   versions → GET /api/Contents/{id}/versions; categories → GET /api/ContentCategories.
   Export from common/public-api.ts. INVARIANT 1: rebuild common.
2. `admin-app/src/app/features/kb/kb-admin.component.{ts,html,spec.ts}` mirroring
   `features/admin/permissions.component.ts` (OnPush + signals + AsyncState + cs-* states).
   US-512 rules: Publish enabled only for Draft; Archive only for non-Archived; both hidden
   when Archived; Archive behind confirm(). Edit disabled when status ≠ Draft (US-511 AC4).
   Version history list newest-first (US-511 AC3).
3. Route (inside authGuard'd shell children, app.routes.ts):
   `{ path: 'kb-admin', canActivate: [roleGuard('Admin','ContentManager')],
      loadComponent: () => import('./features/kb/kb-admin.component') }`
4. NAV_ITEMS (layout/shell.component.ts):
   `{ path: '/kb-admin', key: 'nav.kbAdmin', icon: 'menu_book', adminOnly: true }`
5. translations.ts (en+ar): nav.kbAdmin, kb.title/subtitle/publish/archive/versions/newArticle,
   kb.status.*, kb.empty.

## Tests (failing first, names = AC ids)
AC509_ListShowsStatusFilter · AC510_CreateSubmitsDraft · AC511_EditCreatesNewVersionAndShowsHistory
AC512_PublishArchivePerStatus

## Gate
npx ng build common; npx ng test admin-app --watch=false → all green, output pasted.
