# Task 02 — Profile validation and Identity update boundary

**Criteria:** `AC-432`, `AC-433`, `AC-434`, `AC-435`, `AC-436`, `AC-437`, `AC-438`  
**Commit:** `feat(auth): validate and persist self-service profile updates`

## Files

- Change `backend/src/CustomerSupport.Application/Features/Auth/Commands/UpdateCurrentUserProfile/`.
- Change `backend/src/CustomerSupport.Application/Interfaces/IIdentityUserService.cs` only if a
  current-user-specific operation is required; keep the interface in Application.
- Change `backend/src/CustomerSupport.Infrastructure/Services/IdentityUserService.cs` for the
  Identity adapter implementation.
- Change `backend/src/CustomerSupport.Domain/Entities/Identity/ApplicationUser.cs` only to make the
  invariant explicit; do not add transport or EF concerns.
- Add unit tests under `backend/tests/CustomerSupport.Tests/Unit/Features/Auth/` and integration
  cases to `CurrentUserProfileEndpointTests.cs`.

## Execution steps

1. Write validator tests for empty/whitespace names, exactly 100 characters, 101 characters,
   malformed phone, and non-HTTPS/overlong image URL.
2. Write handler tests proving role/email/password/active/org fields cannot be changed.
3. Normalize trimmed names and phone in the application/domain boundary.
4. If phone changes, reset `PhoneNumberConfirmed` only when the normalized value differs; preserve
   it for an identical number.
5. Map Identity errors to the existing safe field-keyed response contract.

## Live examples

```csharp
// Valid boundary: exactly 100 characters is accepted.
new UpdateCurrentUserProfileRequest("A".PadRight(100, 'A'), "Morgan", null, null);

// Invalid: a client cannot send role/email even if JSON contains them because the request DTO has
// no such properties.
```

## Run

```text
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~UpdateCurrentUserProfile"
```
