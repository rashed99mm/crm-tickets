# FEAT-15 OTP Verification — Implementation Plan

**Spec:** `docs/superpowers/specs/EPIC-09-US-112-otp-verification-design.md`  
**Epic:** `EPIC-09 Security & Administration`  
**Sprint:** `9`  
**Dependency:** `EPIC-12-US-000-notification-gateway`  
**Status:** planned; application implementation not started

## Existing patterns

- `backend/src/CustomerSupport.Application/Contracts/Response.cs`
- `backend/src/CustomerSupport.Application/Messages/IMessageFactory.cs`
- `backend/src/CustomerSupport.Application/Interfaces/ISecretProtector.cs`
- `backend/src/CustomerSupport.Application/Interfaces/IUserContext.cs`
- `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs`
- `backend/src/CustomerSupport.ExternalApi/Controllers/`
- `backend/src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs`

## Contract

```csharp
public enum OtpVerificationType
{
    Email = 1,
    Sms = 2
}

public sealed record RequestOtpCommand(
    string Contact,
    OtpVerificationType Type) : ICommand<Response<RequestOtpResponse>>;

public sealed record VerifyOtpCommand(
    Guid VerificationId,
    string Code) : ICommand<Response<VerifyOtpResponse>>;
```

## Tasks

### Task 1 — Domain model and secure code port

**Files:** `Domain/Entities/Verification/OtpVerification.cs`,
`Domain/Entities/Verification/OtpVerificationType.cs`, `Application/Interfaces/IOtpCodeGenerator.cs`,
EF configuration and migration.

**Steps:**

1. Write domain tests for six-digit policy, expiry, cooldown, failed attempts, refresh, and
   verification state transitions.
2. Add private-set fields for contact, type, hash, expiry, last sent, failed attempts, verified,
   invalidated, and row version/concurrency state.
3. Add `Create`, `CanResend`, `IsExpired`, `RegisterFailedAttempt`, `Refresh`, `MarkVerified`, and
   `Invalidate` methods.
4. Persist only the hash; never add a plaintext-code column.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~OtpVerificationDomain"`  
**Expected:** Domain tests cover boundaries and no plaintext code is present in the model.

**Commit:** `feat: add otp verification domain model`

### Task 2 — Request OTP command and notification dispatch

**Files:** `Application/Features/Verification/Commands/RequestOtp/`,
`Application/Features/Verification/Dtos/`, `ExternalApi/Controllers/VerificationController.cs`.

**Steps:**

1. Write failing handler tests for Email and SMS channel selection, cooldown, invalid contact, and
   gateway failure.
2. Validate normalized email/phone input and enum values server-side.
3. Generate and hash the code, create/refresh the record, and dispatch through:

```csharp
var channel = request.Type == OtpVerificationType.Email
    ? NotificationChannel.Email
    : NotificationChannel.Sms;

await gateway.SendAsync(new NotificationDispatchRequest(
    "OTP_VERIFICATION", null, [channel],
    new Dictionary<string, string> { ["Code"] = plainCode },
    request.Type == OtpVerificationType.Email ? request.Contact : null,
    request.Type == OtpVerificationType.Sms ? request.Contact : null,
    true, deduplicationKey, correlationId), ct);
```

4. Persist only after dispatch acceptance and return expiry/cooldown metadata, never the code.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~RequestOtp"`  
**Expected:** Email and SMS use the gateway; failed delivery leaves no falsely successful response.

**Commit:** `feat: request otp through notification channels`

### Task 3 — Verify OTP and Identity confirmation

**Files:** `Application/Features/Verification/Commands/VerifyOtp/`,
`Infrastructure/Security/OtpCodeGenerator.cs`, Identity update adapter, external controller.

**Steps:**

1. Write failing tests for correct, incorrect, expired, invalidated, and exhausted codes.
2. Load the record with a concurrency token and reject invalid states before comparison.
3. Increment failed attempts transactionally; mark verified only after a successful hash comparison.
4. Update `EmailConfirmed` or `PhoneNumberConfirmed` for a linked Identity user through the
   infrastructure boundary.
5. Return a generic safe failure for all invalid code paths.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~VerifyOtp"`  
**Expected:** Only one concurrent verification succeeds and confirmation state is consistent.

**Commit:** `feat: verify otp and confirm identity contact`

### Task 4 — Rate limiting, public routes, and evidence

**Files:** `ExternalApi/Controllers/VerificationController.cs`, shared rate-limit configuration,
message catalogue, unit/integration tests.

**Steps:**

1. Add public routes `POST /api/verification/request` and `POST /api/verification/verify`.
2. Apply request and verify rate limits by contact/IP without placing the contact in logs.
3. Return account-enumeration-safe messages and standard trace/timestamp envelope fields.
4. Test EmailGateway and SmsGateway failures with fake integration URLs.
5. Inspect logs and database assertions for plaintext OTP leakage.

**Run:** `dotnet build backend/CustomerSupport.slnx --warnaserror` then
`dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~Otp"`  
**Expected:** Clean build and focused OTP tests pass; no code or secret leakage is observed.

**Commit:** `test: evidence otp verification security behavior`
