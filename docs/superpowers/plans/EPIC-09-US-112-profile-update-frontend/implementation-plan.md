# Profile Update and OTP Verification — Frontend Implementation Plan

**Backend dependency:** `docs/superpowers/plans/EPIC-09-US-112-profile-update-and-otp-verification/`  
**Design source:** `stitch_smart_support_ticketing_crm/user_profile_settings/code.html`  
**Application:** `frontend/projects/admin-app`  
**Status:** Planned

## Scope

Replace the current compact password/identity profile page with the Stitch settings workspace while
preserving the existing password action. The page will contain the reference's profile picture
region, personal information form, read-only email row, preferences region, settings sub-navigation,
save/cancel footer and a phone OTP verification step when a phone number changes.

`Job Title` and `Time Zone` appear in the reference but are not represented by the current backend
contract. They must render as explicitly unavailable/read-only until a separate backend schema
decision adds those fields. No fake values are allowed.

## Existing files to read first

- `frontend/projects/admin-app/src/app/features/account/profile.component.ts`
- `frontend/projects/admin-app/src/app/features/account/profile.component.html`
- `frontend/projects/admin-app/src/app/app.routes.ts` (`/profile` route at lines 108–112)
- `frontend/projects/admin-app/src/app/layout/shell.component.html` (profile identity link)
- `frontend/projects/common/src/lib/auth/staff.api.ts`
- `frontend/projects/common/src/lib/auth/session.store.ts`
- `frontend/projects/common/src/lib/ui/input-field.component.{ts,html}`
- `frontend/projects/common/src/lib/ui/button.component.{ts,html}`
- `frontend/projects/common/src/lib/ui/placeholder.component.{ts,html}`
- `frontend/projects/common/src/lib/i18n/translations.ts`
- `frontend/projects/common/src/lib/testing/rtl-safety.spec.ts`
- `frontend/projects/common/src/lib/testing/no-hardcoded-strings.spec.ts`

## Files to change or add

| File | Purpose |
|---|---|
| `frontend/projects/common/src/lib/auth/staff.api.ts` | Add typed `getCurrentProfile`, `updateCurrentProfile`, and OTP verify/request methods or move them to a new `profile.api.ts` if ownership is clearer |
| `frontend/projects/common/src/lib/auth/staff.api.spec.ts` | Assert method, URL, body and unwrapped response |
| `frontend/projects/admin-app/src/app/features/account/profile.component.ts` | Signals, typed forms, API calls, OTP step and save state |
| `frontend/projects/admin-app/src/app/features/account/profile.component.html` | Stitch composition ported to Angular control flow and translated strings |
| `frontend/projects/admin-app/src/app/features/account/profile.component.spec.ts` | Add missing component test file; cover criteria and DOM states |
| `frontend/projects/common/src/lib/i18n/translations.ts` | Add every new profile/OTP label in English and Arabic |
| `frontend/projects/common/src/lib/testing/rtl-safety.spec.ts` | Extend only if new inline markup needs coverage |
| `frontend/e2e/profile-update.spec.ts` | Real backend profile update and OTP verification journey, after backend is available |

## Design translation rules

1. Remove CDN Tailwind config from the reference; use existing Tailwind v4 tokens from
   `frontend/projects/common/src/styles/theme.css`.
2. Replace physical utilities from the reference (`pl-*`, `pr-*`, `left-*`) with logical utilities
   (`ps-*`, `pe-*`, `start-*`, `end-*`, `text-start`, `text-end`).
3. Preserve the reference structure: top navigation, 280px desktop sidebar, inner settings rail,
   main content cards and bottom action row.
4. At mobile widths, collapse the outer shell through the existing shell drawer, make the inner
   settings rail horizontally scrollable, and stack the profile image/form/card sections.
5. Keep form behaviour in TypeScript. Templates use `@if`, `@for` with `track`, and signals only.

## Live implementation example

The current component owns a password form. Extend it with a separate profile form rather than
mixing passwords with profile data:

```ts
readonly profileForm = new FormGroup({
  firstName: new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(100)],
  }),
  lastName: new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(100)],
  }),
  phoneNumber: new FormControl<string | null>(null),
  profileImageUrl: new FormControl<string | null>(null),
});
```

The API call remains typed and the server is authoritative:

```ts
this.staffApi.updateCurrentProfile(this.profileForm.getRawValue()).subscribe({
  next: (profile) => {
    this.profile.set(profile);
    this.requiresPhoneVerification.set(
      profile.phoneNumber !== previousPhone && !profile.phoneNumberConfirmed,
    );
  },
  error: (failure: unknown) => this.error.set(this.toApiError(failure)),
});
```

The template keeps email read-only and makes the OTP step explicit:

```html
<input id="email" [value]="profile().email" readonly aria-describedby="email-help" />
<p id="email-help">{{ 'profile.emailReadOnly' | t }}</p>
@if (requiresPhoneVerification()) {
  <button type="button" (click)="verifyPhone()">{{ 'profile.verifyPhone' | t }}</button>
}
```

Use `ApiError.fieldError('firstName')` and `ApiError.fieldError('phoneNumber')` through
`CsInputField`; do not show field failures only in a global banner.

## Test-first execution

Before each task, add the test named in that task and run it once to observe failure. Implement only
enough code to pass it, then run the focused suite. Query controls by label/role in tests, not by
Tailwind class.

## Commands

From `frontend/`:

```text
npx ng test common --watch=false --include='**/auth/staff.api.spec.ts'
npx ng test admin-app --watch=false --include='**/features/account/profile.component.spec.ts'
npx ng test common --watch=false --include='**/testing/*.spec.ts'
npx ng build admin-app
npx playwright test profile-update
```

Run the portal suite only if the backend contract explicitly adds a portal profile route. Do not
duplicate staff profile code in `portal-app` without a real portal requirement.
