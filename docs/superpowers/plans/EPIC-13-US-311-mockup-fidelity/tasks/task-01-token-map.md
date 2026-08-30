# Task 01 · Audit references and freeze token map

**Criteria:** `AC-400`, `AC-401`, `AC-402`, `AC-403`, `AC-404`  
**Status:** Completed (152/152 tests passed in ng test common)

## Changes

1. Inventory every target `code.html`, its fonts, icons, colour tokens, layout primitives and
   responsive classes.
2. Compare the reference values with `common/src/styles/theme.css` and document deliberate changes.
3. Add scoped Command Center and Proton token groups without duplicating component rules.
4. Add tests proving palette resolution, semantic chip contrast and RTL-safe utility usage.

## Test-first cases

- `AC400_SharedTokenSourceIsUsedByAdaptedScreens`
- `AC401_CommandCenterScreenUsesCommandCenterTokens`
- `AC402_ProtonScreenUsesProtonTokens`
- `AC403_StatusAndPriorityRemainSemanticInBothPalettes`
- `AC404_ArabicLayoutUsesLogicalDirectionUtilities`

## Done when

The token map is reviewed against all references, focused tests pass, and no screen component owns a
duplicate palette definition.

## Exact files

- Read: `stitch_smart_support_ticketing_crm/command_center/DESIGN.md` and
  `stitch_smart_support_ticketing_crm/proton_precision/DESIGN.md`.
- Read/change: `frontend/projects/common/src/styles/theme.css`.
- Read/change tests: `frontend/projects/common/src/lib/ui/status-pill.component.spec.ts`,
  `frontend/projects/common/src/lib/testing/rtl-safety.spec.ts`.
- Read/change exports: `frontend/projects/common/src/public-api.ts` only if a new shared token helper
  is actually required.

## Live implementation example

Add a Proton scope to `theme.css`, for example:

```css
[data-design-system='proton'] {
  --color-primary: #000000;
  --color-primary-container: #171717;
  --color-surface: #f8fafc;
  --color-on-surface: #111827;
}
```

Do not place `bg-black` directly in a ticket component. The component should continue using
`bg-primary`, allowing the shell attribute to select the mockup system.

## Execution commands

```text
cd frontend
npx ng test common --watch=false --include='**/status-pill.component.spec.ts'
npx ng test common --watch=false --include='**/rtl-safety.spec.ts'
```
