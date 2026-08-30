# Task 03 — OTP verification domain and repository

**Criteria:** `AC-439`, `AC-440`, `AC-441`, `AC-442`, `AC-443`, `AC-445`  
**Dependency:** approved `EPIC-09-US-112-otp-verification-design.md` request flow  
**Commit:** `feat(security): add concurrent-safe otp verification state`

## Files

- Add/change `backend/src/CustomerSupport.Domain/Entities/Verification/OtpVerification.cs` and
  `OtpVerificationType.cs`.
- Add `backend/src/CustomerSupport.Application/Interfaces/IOtpVerificationRepository.cs` and any
  identity-confirmation port required by the existing architecture.
- Add `backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/OtpVerificationConfiguration.cs`.
- Change `backend/src/CustomerSupport.Infrastructure/Persistence/AppDbContext.cs`.
- Add unit tests under `backend/tests/CustomerSupport.Tests/Unit/Domain/`.

## Execution steps

1. Write domain tests for six-digit validation, expiry, verified/invalidated/locked states and the
   fifth failed attempt boundary.
2. Store `CodeHash`, never `Code`; use a concurrency token (`rowversion` or equivalent).
3. Make `RegisterFailedAttempt` and `MarkVerified` intention-revealing and private-set.
4. Add repository methods that load the record without exposing another user’s existence.
5. Add the EF configuration with explicit lengths, indexes and concurrency mapping.

## Live invariant example

```csharp
if (verification.IsExpired(clock.UtcNow) || verification.IsLocked || verification.IsVerified)
    return OtpVerificationResult.Invalid;

verification.RegisterFailedAttempt(); // fifth failure locks; sixth never compares
```

## Run

```text
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~OtpVerificationDomain"
```
