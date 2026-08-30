# Task 06 — Visual and regression evidence

**Criteria:** `AC-447`  
**Reference:** `stitch_smart_support_ticketing_crm/user_profile_settings/{code.html,screen.png}`  
**Commit:** `test(frontend): verify profile visual and functional contract`

## Exact files

- Add `frontend/e2e/profile-update.spec.ts`.
- Use `frontend/playwright.config.ts`.
- Update `docs/superpowers/plans/EPIC-09-US-112-profile-update-frontend/README.md` with evidence.
- Update `docs/superpowers/plans/EPIC-09-US-112-profile-update-and-otp-verification/README.md` only for
  the backend dependency result; do not mark backend criteria from frontend tests.

## Steps

1. Capture `/profile` at 375, 768, 1280 and 1920 pixels.
2. Compare with the Stitch reference and record every accepted data-driven deviation.
3. Run common, admin and any changed portal tests.
4. Build `admin-app`; run the real profile/OTP journey against the backend.
5. Confirm no plaintext OTP, token or password appears in DOM screenshots, console output or test
   logs.

## Live screenshot example

```ts
for (const width of [375, 768, 1280, 1920]) {
  await page.setViewportSize({ width, height: 1000 });
  await page.goto('/profile');
  await expect(page).toHaveScreenshot(`profile-settings-${width}.png`, { fullPage: true });
}
```

## Run

```text
npx ng test common --watch=false
npx ng test admin-app --watch=false
npx ng build admin-app
npx playwright test profile-update
```
