# Task 04 — OTP verification application handler

**Criteria:** `AC-439`, `AC-440`, `AC-441`, `AC-442`, `AC-443`, `AC-444`, `AC-445`  
**Commit:** `feat(security): verify otp and confirm identity contact`

## Files

- Add `backend/src/CustomerSupport.Application/Features/Verification/Commands/VerifyOtp/VerifyOtpCommand.cs`.
- Add `VerifyOtpRequest.cs`, `VerifyOtpCommandHandler.cs`, validator and response DTO under the
  same feature folder.
- Change/add Application ports under `backend/src/CustomerSupport.Application/Interfaces/`.
- Implement adapters in `backend/src/CustomerSupport.Infrastructure/Services/` and wire them in
  `ServiceCollectionExtensions.cs`.
- Add unit tests under `backend/tests/CustomerSupport.Tests/Unit/Features/Verification/`.

## Execution steps

1. Write failing handler tests for correct, malformed, wrong, expired, locked and already verified
   codes using a fake clock, fake repository and fake identity confirmer.
2. Normalize the code and validate exactly six ASCII digits before hashing.
3. Load by verification id and authenticated user/contact scope; return one safe failure for all
   invalid states.
4. Compare the one-way hash, update attempt state and confirmation atomically.
5. Handle a concurrency conflict as a safe verification failure or idempotent already-completed
   result according to the approved contract; never report two successes.

## Live test example

```csharp
[Fact] // AC-440
public async Task VerifyOtp_WrongCode_DoesNotConfirmPhone()
{
    var result = await handler.Handle(new VerifyOtpCommand(id, "000000"), CancellationToken.None);
    result.Success.Should().BeFalse();
    identity.PhoneNumberConfirmed.Should().BeFalse();
}
```

## Run

```text
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~VerifyOtp"
```
