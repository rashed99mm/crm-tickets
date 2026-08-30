# Task 01 — Profile API client and models

**Criteria:** `AC-430`, `AC-436`, `AC-446`  
**Dependency:** backend profile endpoint contract is implemented  
**Commit:** `feat(frontend): add current profile API client`

## Exact files

- Change `frontend/projects/common/src/lib/auth/staff.api.ts`.
- Add `frontend/projects/common/src/lib/auth/staff.api.spec.ts`; this file does not exist yet.
- Change `frontend/projects/common/src/public-api.ts` only if a new `profile.api.ts` is exported.
- Read backend contract: `backend/src/CustomerSupport.Application/Features/Auth/Dtos/AuthDtos.cs`
  and `backend/src/CustomerSupport.InternalApi/Controllers/AuthController.cs`.

## Steps

1. Add the frontend `UserInfoDto` shape matching the serialized backend DTO, including
   `phoneNumber`, `emailConfirmed` and `phoneNumberConfirmed` if the backend exposes the latter.
2. Add `getCurrentProfile()` for `GET /api/Auth/me`.
3. Add `updateCurrentProfile(request)` for `PUT /api/Auth/me`.
4. Assert the request body contains only the four writable profile fields.
5. Do not access `success`, `code` or `data` in the component; the envelope interceptor unwraps it.

## Live test example

```ts
api.updateCurrentProfile({
  firstName: 'Alex', lastName: 'Morgan', phoneNumber: null, profileImageUrl: null,
}).subscribe();
const request = http.expectOne('/api/Auth/me');
expect(request.request.method).toBe('PUT');
expect(request.request.body).toEqual({
  firstName: 'Alex', lastName: 'Morgan', phoneNumber: null, profileImageUrl: null,
});
```

## Run

```text
npx ng test common --watch=false --include='**/auth/staff.api.spec.ts'
```
