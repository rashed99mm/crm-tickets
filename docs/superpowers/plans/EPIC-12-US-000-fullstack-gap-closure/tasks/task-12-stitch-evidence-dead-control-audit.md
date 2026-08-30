# Task 12 - Stitch Evidence Dead Control Audit

**Status:** Ready  
**Closes gaps:** All remaining dead navigation/decorative controls and visual fidelity risk.

## Files

- All changed Angular templates.
- `frontend/e2e/**`
- `docs/assessment/rubric-traceability.md`
- `docs/requirements/delivery-plan.md`

## Implementation

- Run route audit for every `routerLink` and navigation action.
- Run template audit for buttons without `(click)`, `(pressed)`, `routerLink`, `href`, or `type="submit"`.
- Run hardcoded data audit for static metrics/API keys.
- Capture Playwright screenshots for Stitch reference pages at desktop and mobile.
- Update rubric and delivery plan from observed evidence only.

## Code Example

```ts
test('admin navigation has no dead primary routes', async ({ page }) => {
  await page.goto('/dashboard');
  for (const href of ['/customers', '/tickets', '/settings', '/users']) {
    await page.locator(`a[href="${href}"]`).click();
    await expect(page).not.toHaveURL(/forbidden|404/);
  }
});
```

## Acceptance

- [ ] No primary button is decorative.
- [ ] No admin route link 404s.
- [ ] No static API key or static KPI literal remains.
- [ ] Stitch screenshots pass desktop/mobile review.
- [ ] Full backend, frontend, build, and E2E gates are recorded.

## Evidence

Pending.
