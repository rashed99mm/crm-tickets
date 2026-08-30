# Task 03 — Verify OTP

**Criteria:** `OTP-4`, `OTP-5`, `OTP-6`, `OTP-8`

## Files

- `Application/Features/Verification/Commands/VerifyOtp/*`
- `Infrastructure/Security/OtpCodeGenerator.cs`
- Identity confirmation adapter and verification controller.

## Steps

1. Write failing tests for success, wrong code, expired code, invalidated code, and max attempts.
2. Compare supplied code with the stored hash using the secure generator port.
3. Increment attempts and save atomically on failure.
4. Mark the record verified and update the linked Identity confirmation flag on success.
5. Add a race test proving two concurrent requests cannot both complete the same record.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~VerifyOtp"`  
**Expected:** Invalid paths never update account confirmation and only one concurrent success exists.

**Commit:** `feat: implement otp verification command`
