# Task 02 — Request OTP

**Criteria:** `OTP-1`, `OTP-2`, `OTP-3`, `OTP-9`

## Files

- `Application/Features/Verification/Commands/RequestOtp/*`
- `Application/Features/Verification/Dtos/RequestOtpResponse.cs`
- `ExternalApi/Controllers/VerificationController.cs`

## Steps

1. Write failing handler tests for Email and SMS dispatch through `INotificationGateway`.
2. Normalize and validate contact input; reject unsupported channel values.
3. Enforce cooldown before generating a new code.
4. Generate a secure code, hash it, and dispatch the plaintext only inside the gateway call.
5. Return only verification ID, expiry, and cooldown metadata in `Response<T>`.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~RequestOtp"`  
**Expected:** Both integration URLs are selected through the gateway and failures are safe.

**Commit:** `feat: implement otp request command`
