# Task 02 · Build responsive shared shell primitives

**Criteria:** `AC-405`, `AC-413`, `AC-414`, `AC-415`, `AC-418`  
**Status:** Completed (All shell tests passed in admin-app and portal-app)

## Changes

1. Adapt the shared staff/portal shell, header, sidebar rail, active nav item and page canvas.
2. Add the mobile drawer and tablet navigation behaviour with keyboard focus restoration.
3. Extract only repeated patterns into `common`: page heading, state region, table frame, drawer and
   responsive layout helpers.
4. Preserve existing route and session signal behaviour.

## Test-first cases

- `AC405_DesktopShellMatchesCommandCenterComposition`
- `AC413_MobileShellUsesAccessibleDrawerWithoutOverflow`
- `AC414_TabletShellUsesTabletNavigation`
- `AC415_DesktopShellPreservesGuttersAndMaxWidth`
- `AC418_DrawerAndNavigationHaveKeyboardFocusManagement`

## Done when

Both apps render the shared shell at all target widths, existing route tests remain green, and the
shell has no physical-direction utilities.

## Exact files

- Read/change: `frontend/projects/admin-app/src/app/layout/shell.component.ts` and
  `shell.component.html`.
- Read/change tests: `frontend/projects/admin-app/src/app/layout/shell.component.spec.ts` and
  `nav-routes.spec.ts`.
- Read/change portal equivalent: `frontend/projects/portal-app/src/app/layout/shell.component.ts`,
  `shell.component.html`, `shell.component.spec.ts`.
- Reuse: `frontend/projects/common/src/lib/ui/icon.component.*`, `button.component.*`,
  `language-switcher.component.*`.

## Live implementation example

Keep `NAV_ITEMS` in `admin-app/src/app/layout/shell.component.ts` as the route source of truth. Add
one signal such as `mobileMenuOpen = signal(false)` and bind the existing menu button in
`shell.component.html` to it. At `<lg`, render the same nav links inside an `aside` drawer; on
Escape close it and return focus to the trigger. Do not create a second navigation array for mobile.

## Execution commands

```text
cd frontend
npx ng test admin-app --watch=false --include='**/layout/shell.component.spec.ts'
npx ng test admin-app --watch=false --include='**/layout/nav-routes.spec.ts'
npx ng test portal-app --watch=false --include='**/layout/shell.component.spec.ts'
```
