# Task 04 — OTP request and verify UI flow

**Criteria:** `AC-436`, `AC-439`, `AC-440`, `AC-441`, `AC-442`, `AC-443`, `AC-444`, `AC-445`, `AC-447`  
**Dependency:** backend OTP request/verify endpoints and response DTO are implemented  
**Commit:** `feat(frontend): verify changed phone with otp`

## Exact files

- Change/add `frontend/projects/common/src/lib/auth/staff.api.ts` or a dedicated
  `frontend/projects/common/src/lib/auth/verification.api.ts`.
- Add corresponding HTTP tests beside the API file.
- Change `frontend/projects/admin-app/src/app/features/account/profile.component.ts` and `.html`.
- Extend `profile.component.spec.ts`.
- Add OTP translation keys in `frontend/projects/common/src/lib/i18n/translations.ts`.

## Steps

1. Add a six-digit OTP form with `Validators.pattern(/^[0-9]{6}$/)` and a label for every input.
2. Request an OTP only after the profile update response identifies an unconfirmed changed phone.
3. Send `{ verificationId, code }` to the documented endpoint; never log or render the code except
   in the input while the user is entering it.
4. On success refresh `GET /api/Auth/me` and show confirmed state.
5. On wrong/expired/locked failure display the safe server message without distinguishing the cause.
6. Disable submit while busy and prevent duplicate requests.

## Live test example

```ts
it('AC440_WrongOtpShowsSafeErrorAndDoesNotMarkPhoneVerified', () => {
  component.otpForm.setValue({ code: '000000' });
  component.verifyPhone();
  const request = http.expectOne('/api/verification/verify');
  expect(request.request.body.code).toBe('000000');
  request.flush({ code: 'OTP_INVALID', message: { en: 'Verification failed', ar: 'فشل التحقق' } },
    { status: 400, statusText: 'Bad Request' });
  expect(component.profile()?.phoneNumberConfirmed).toBe(false);
});
```

## Run

```text
npx ng test common --watch=false --include='**/*verification*.spec.ts'
npx ng test admin-app --watch=false --include='**/features/account/profile.component.spec.ts'
```
