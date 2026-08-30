# Task 01 — OTP Domain and Persistence

**Criteria:** `OTP-1`, `OTP-2`, `OTP-3`, `OTP-5`, `OTP-6`

## Files

- `Domain/Entities/Verification/OtpVerification.cs`
- `Domain/Entities/Verification/OtpVerificationType.cs`
- `Infrastructure/Persistence/Configurations/OtpVerificationConfiguration.cs`
- Migration and model snapshot.

## Steps

1. Write failing domain tests for six-digit policy, five-minute expiry, 60-second cooldown, and five
   failed attempts.
2. Store only `CodeHash`, never plaintext `Code`.
3. Add UTC timestamps, private state transitions, and optimistic concurrency.
4. Review migration `Up`/`Down` and add indexes for contact/type/active lookup.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~OtpVerificationDomain"`  
**Expected:** Boundary tests pass and schema contains no plaintext code field.

**Commit:** `feat: persist secure otp verification state`
