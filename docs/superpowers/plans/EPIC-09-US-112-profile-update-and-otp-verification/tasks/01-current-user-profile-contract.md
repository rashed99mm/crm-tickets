# Task 01 — Current-user profile contract

**Criteria:** `AC-430`, `AC-431`, `AC-432`, `AC-438`, `AC-446`  
**Commit:** `feat(auth): add current-user profile update contract`

## Files

- Add `backend/src/CustomerSupport.Application/Features/Auth/Commands/UpdateCurrentUserProfile/UpdateCurrentUserProfileCommand.cs`.
- Add `UpdateCurrentUserProfileRequest.cs`, handler and response/validator in the same folder.
- Change `backend/src/CustomerSupport.InternalApi/Controllers/AuthController.cs`.
- Reuse `backend/src/CustomerSupport.Application/Features/Auth/Dtos/AuthDtos.cs` and
  `GetCurrentUserQueryHandler.cs` for the projection shape.
- Add `backend/tests/CustomerSupport.Tests/Integration/CurrentUserProfileEndpointTests.cs`.

## Execution steps

1. Add failing integration tests for authenticated `PUT /api/Auth/me`, unauthenticated `401`, and
   response envelope/DTO shape.
2. Add `[HttpPut("me")]` with `[Authorize]` to `AuthController`; obtain identity from
   `User.GetRequiredUserId()` or the application `IUserContext`, never the body.
3. Send the command and return `UserInfoDto`; do not return `AuthResponse` or tokens.
4. Assert a body containing `id` is rejected/ignored by explicit DTO binding and cannot change a
   second user.

## Live test example

```csharp
var response = await client.PutAsJsonAsync("/api/Auth/me", new {
    firstName = "Alex", lastName = "Morgan", phoneNumber = (string?)null,
    profileImageUrl = (string?)null, id = otherUserId
});
response.StatusCode.Should().Be(HttpStatusCode.OK);
// Re-read otherUserId and assert its names are unchanged.
```

## Run

```text
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CurrentUserProfileEndpointTests"
```
