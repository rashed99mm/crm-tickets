# T1 — Responsive Layout Contract and Failing Tests

**Story:** `US-311`  
**Criteria:** AC-311.1, AC-311.2, AC-311.3; original AC-22  
**Status:** not started  
**Commit:** pending  
**Test evidence:** none; not run by instruction

## Files

- `frontend/projects/admin-app/src/app/layout/shell.component.ts` and `.html`;
- `frontend/projects/portal-app/src/app/layout/shell.component.ts` and `.html`;
- `frontend/projects/admin-app/src/styles.css`, `frontend/projects/portal-app/src/styles.css`,
  `frontend/projects/common/src/styles/theme.css`;
- new `frontend/e2e/responsive-layout.spec.ts`;
- affected `*.component.spec.ts` files beside shells, dashboard, queues, details, and forms.

## Work

1. Define mobile `<768px`, tablet `768px–1024px`, and desktop `>1024px` CSS rules using existing
   Tailwind/theme tokens. Do not add resize JavaScript.
2. Add failing tests named `AC311_1_NoPageOverflowAt375`, `AC311_1_NoPageOverflowAt768`,
   `AC311_1_NoPageOverflowAt1440`, `AC311_2_MobileMenuIsKeyboardReachable`, and
   `AC311_3_TableAndFormKeepFocusOrder`.
3. Specify the menu's button name, `aria-expanded`, `aria-controls`, Escape behavior, focus return,
   and overlay dismissal before implementing it.
4. Exercise staff and portal screens, including loading, empty, error, forms, pagination, and table
   states. Preserve content and keyboard order rather than merely making screenshots fit.

## Later verification

```powershell
cd frontend
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng test portal-app --watch=false
npx ng build admin-app
npx ng build portal-app
npx playwright test e2e/responsive-layout.spec.ts
```

## Evidence / deviations

**Evidence:** pending failing-test and later green-test output.  
**Deviations:** none.
