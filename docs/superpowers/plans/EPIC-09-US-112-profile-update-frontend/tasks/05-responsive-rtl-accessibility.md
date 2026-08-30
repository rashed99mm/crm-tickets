# Task 05 — Responsive, RTL and accessibility hardening

**Criteria:** `AC-223`, `AC-237`, `AC-447`  
**Commit:** `fix(frontend): harden profile responsive and rtl layout`

## Exact files

- `frontend/projects/admin-app/src/app/features/account/profile.component.html`
- `frontend/projects/admin-app/src/app/features/account/profile.component.spec.ts`
- `frontend/projects/common/src/lib/testing/rtl-safety.spec.ts`
- `frontend/projects/common/src/lib/testing/no-hardcoded-strings.spec.ts`
- `frontend/projects/common/src/lib/i18n/bilingual-ui.spec.ts`

## Steps

1. Verify 375px: no horizontal page overflow; settings tabs scroll within their own rail; cards and
   actions stack; every button remains reachable.
2. Verify 768px: profile photo/card and fields use the tablet two-column layout from the reference.
3. Verify 1280px/1920px: 280px outer sidebar, 256px inner rail and max-width content match reference.
4. Switch locale to Arabic and assert logical spacing, alignment, borders and direction.
5. Query all controls by accessible name and assert email read-only semantics and OTP label.

## Live Playwright assertion

```ts
await page.setViewportSize({ width: 375, height: 900 });
await page.goto('/profile');
expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
await expect(page.getByLabel('First name')).toBeVisible();
await expect(page.getByLabel('Email address')).toBeDisabled();
```

## Run

```text
npx ng test common --watch=false --include='**/testing/*.spec.ts'
npx ng test admin-app --watch=false --include='**/features/account/profile.component.spec.ts'
npx playwright test profile-update --grep='responsive|rtl|accessibility'
```
