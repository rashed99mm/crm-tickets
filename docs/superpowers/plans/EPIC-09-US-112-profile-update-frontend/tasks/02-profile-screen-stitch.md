# Task 02 — Profile screen structure and Stitch styling

**Criteria:** `AC-447`  
**Reference:** `stitch_smart_support_ticketing_crm/user_profile_settings/code.html`  
**Commit:** `feat(frontend): adapt profile screen to stitch settings layout`

## Exact files

- Change `frontend/projects/admin-app/src/app/features/account/profile.component.html`.
- Change `frontend/projects/admin-app/src/app/features/account/profile.component.ts` only for
  display signals/imports.
- Add `frontend/projects/admin-app/src/app/features/account/profile.component.spec.ts`.
- Change `frontend/projects/common/src/lib/i18n/translations.ts`.
- Read `frontend/projects/common/src/styles/theme.css` for token names.

## Steps

1. Port the reference hierarchy: settings page heading, inner settings tabs, profile picture card,
   personal information card, preferences card and action footer.
2. Replace reference hardcoded text with translation keys.
3. Keep email disabled/read-only and display the reference help text.
4. Use `CsPlaceholder` for unsupported Job Title and Time Zone fields.
5. Preserve the existing password card below or in the Security tab; do not remove password change.

## Live markup example

```html
<main class="mx-auto flex w-full max-w-5xl flex-col gap-4 md:flex-row">
  <aside class="w-full shrink-0 md:w-64">
    <nav class="flex gap-1 overflow-x-auto md:flex-col" aria-label="Profile settings">
      <button type="button" class="...">{{ 'profile.general' | t }}</button>
      <button type="button" class="...">{{ 'profile.security' | t }}</button>
    </nav>
  </aside>
  <section class="min-w-0 flex-1 space-y-6">...</section>
</main>
```

## Test-first cases

- `AC447_ProfileRendersReferenceSettingsRegions`
- `AC447_EmailIsReadOnlyAndUnsupportedFieldsAreUnavailable`
- `AC447_ProfileTextUsesTranslationKeys`

## Run

```text
npx ng test admin-app --watch=false --include='**/features/account/profile.component.spec.ts'
```
