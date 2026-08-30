# Profile Update and OTP Verification — Backend Implementation Plan

**Spec:** `docs/superpowers/specs/EPIC-09-US-112-profile-update-and-otp-verification-design.md`  
**Status:** Planned; implementation is blocked until the spec is approved  
**Backend root:** `backend/`

## Existing files to preserve

- `backend/src/CustomerSupport.Application/Features/Auth/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs`
- `backend/src/CustomerSupport.Application/Features/Auth/Dtos/AuthDtos.cs`
- `backend/src/CustomerSupport.Application/Interfaces/IUserContext.cs`
- `backend/src/CustomerSupport.Application/Interfaces/IIdentityUserService.cs`
- `backend/src/CustomerSupport.Domain/Entities/Identity/ApplicationUser.cs`
- `backend/src/CustomerSupport.InternalApi/Controllers/AuthController.cs`
- `backend/src/CustomerSupport.InternalApi/Controllers/UsersController.cs`
- `backend/src/CustomerSupport.Infrastructure/Services/IdentityUserService.cs`
- `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs`
- `backend/tests/CustomerSupport.Tests/Integration/CrmApiFactory.cs`
- `backend/tests/CustomerSupport.Tests/Integration/StaffAdministrationTests.cs`

## API contract to implement

```http
GET /api/Auth/me
PUT /api/Auth/me
Content-Type: application/json
Authorization: Bearer <access-token>
```

```json
{
  "firstName": "Alex",
  "lastName": "Morgan",
  "phoneNumber": "+14155550100",
  "profileImageUrl": "https://cdn.example.test/avatar.png"
}
```

The response is `Response<UserInfoDto>` using the existing envelope. The request has no `id`,
`email`, `username`, `roles`, `isActive`, `password`, `departmentId` or `branchId` member.

```http
POST /api/verification/verify
Content-Type: application/json
```

```json
{ "verificationId": "<guid>", "code": "123456" }
```

The response contains only a safe success/result DTO. The plaintext code is never returned.

## Ordered execution

Every task follows: write the failing test named with its `AC-*`, run it and observe failure, make the
smallest implementation change, run focused tests, inspect the diff, then record output in
`README.md`. Do not generate or apply a migration until its `Up` and `Down` methods have been read.

## Task files

1. `tasks/01-current-user-profile-contract.md`
2. `tasks/02-profile-validation-identity.md`
3. `tasks/03-otp-domain-repository.md`
4. `tasks/04-otp-verification-handler.md`
5. `tasks/05-verification-controller-contract.md`
6. `tasks/06-migration-security-evidence.md`

## Verification commands

From `backend/`:

```text
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CurrentUserProfile"
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OtpVerification"
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~Verification"
dotnet build CustomerSupport.slnx --warnaserror
dotnet test CustomerSupport.slnx
```

The full suite and build output must be pasted into the task record before claiming completion.
