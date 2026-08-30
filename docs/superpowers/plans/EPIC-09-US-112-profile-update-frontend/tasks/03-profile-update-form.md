# Task 03 — Profile update form and states

**Criteria:** `AC-430`, `AC-432`, `AC-433`, `AC-434`, `AC-435`, `AC-436`, `AC-437`, `AC-438`, `AC-447`  
**Commit:** `feat(frontend): wire profile update form and server errors`

## Exact files

- Change `frontend/projects/admin-app/src/app/features/account/profile.component.ts`.
- Change `profile.component.html`.
- Add/update `profile.component.spec.ts`.
- Reuse `frontend/projects/common/src/lib/ui/input-field.component.*`, `button.component.*`,
  `error-state.component.*` and `loading-state.component.*`.

## Steps

1. Write tests for initial profile hydration, valid save, invalid client validation, disabled submit
   while busy, server field errors, success reset and API failure state.
2. Load `GET /api/Auth/me` into an explicit signal state; do not use an empty profile for errors.
3. Populate the typed form from the response.
4. Submit only when valid and not busy; map `ApiError.fieldError` to the matching controls.
5. On success update the local profile signal and show a translated status message.
6. If phone changed and is unconfirmed, open the OTP verification step instead of showing it as
   verified.

## Live test example

```ts
it('AC430_SubmitsOnlyWhenValidAndMapsTheUpdatedProfile', () => {
  component.profileForm.setValue({
    firstName: 'Alex', lastName: 'Morgan', phoneNumber: '+14155550100', profileImageUrl: null,
  });
  component.saveProfile();
  const request = http.expectOne('/api/Auth/me');
  expect(request.request.method).toBe('PUT');
  request.flush(updatedProfileEnvelope);
  expect(component.profile()?.firstName).toBe('Alex');
});
```

## Run

```text
npx ng test admin-app --watch=false --include='**/features/account/profile.component.spec.ts'
```
