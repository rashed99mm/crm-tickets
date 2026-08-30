# T1 — RTL Inventory and Failing Tests

**Story:** `US-312`  
**Criteria:** AC-312.1, AC-312.2; original AC-23  
**Status:** not started  
**Commit:** pending  
**Test evidence:** none; not run by instruction

## Files

- `frontend/projects/common/src/lib/i18n/locale.store.ts`;
- `frontend/projects/common/src/lib/testing/rtl-safety.spec.ts`;
- `frontend/projects/common/src/lib/ui/` directional components;
- both app `layout/shell.component.html` files and affected feature templates;
- `frontend/projects/admin-app/src/app/layout/shell.component.spec.ts` and
  `frontend/projects/portal-app/src/app/layout/shell.component.spec.ts`.

## Work

1. Inventory physical utilities/CSS and classify each as a bug or documented non-directional asset.
2. Add failing tests named `AC312_1_ArabicSetsHtmlDirAndLang`,
   `AC312_1_SidebarIsAtInlineEnd`, `AC312_1_FormsAndTablesUseLogicalAlignment`, and
   `AC312_2_DirectionalIconsMirrorOnlyWhenSemantic`.
3. Assert inline templates separately because the current safety scanner covers `.html` files only.
4. Define focus and reading order for the shell menu, forms, tables, breadcrumbs, and pagination.
5. Implement with logical properties and semantic icon transforms only; do not alter Arabic copy.

## Later verification

```powershell
cd frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test --grep "RTL|Arabic|rtl|arabic"
```

## Evidence / deviations

**Evidence:** pending test output and Arabic viewport screenshots.  
**Deviations:** none.
