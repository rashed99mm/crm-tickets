# Task 05 - Knowledge Base Completion

**Status:** Ready  
**Closes gaps:** Version history not rendered, category picker missing, insights card static.

## Files

- Backend: existing `ContentsController.cs`, `ContentCategoriesController.cs`, `ContentVersion.cs`
- Frontend API: `common/src/lib/admin/kb-admin.api.ts`
- Frontend UI: `admin-app/src/app/features/kb/kb-admin.component.*`

## Implementation

- Ensure versions endpoint includes author/timestamp/change summary.
- Render versions in edit mode.
- Add category picker to create/edit.
- Persist category assignment.
- Add KB analytics endpoint if loaded page is not enough for global insights.

## Code Example

```ts
this.api.create(request)
  .pipe(switchMap(created => this.api.assignCategory(created.id, selectedCategoryId)))
  .subscribe(() => this.load());
```

## Acceptance

- [ ] Create/edit includes category picker.
- [ ] Category persists and reloads in article table.
- [ ] Version history renders newest first.
- [ ] Insights are API-derived or computed from loaded data.
- [ ] No hardcoded insight literals remain.

## Evidence

Pending.
