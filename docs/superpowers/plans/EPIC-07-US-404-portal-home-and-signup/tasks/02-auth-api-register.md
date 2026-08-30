# Task 02 — `AuthApi.register` + contract spec test (common)

**Story:** `US-401` · **Criteria:** `ASG-5`, `ASG-8` (client contract)
**Status:** done; verified by the end-of-work test run

## Files

- Modify `frontend/projects/common/src/lib/auth/auth.api.ts` — add `RegisterRequest` interface
  `{ email, username, password, firstName, lastName, phoneNumber: string | null }` and
  `register(payload): Observable<{ id: string }>` posting `POST /api/Auth/register`.
- Modify `frontend/projects/common/src/lib/auth/auth.api.spec.ts` — `HttpTestingController` test
  asserting the request URL, method, and body (phone sent as `null` when blank).

## Implementation sequence

1. Add the `register` method and `RegisterRequest` type to `AuthApi` (blank phone → `null`, never `""`).
2. Spec: assert `expectOne('/api/Auth/register')`, POST, exact body; export the type from
   `public-api.ts` if a feature needs it.

## Tests and evidence

`ASG5_Register_PostsContract_WithNullPhone` (and a populated-phone variant). Run with the common
suite at the end.
