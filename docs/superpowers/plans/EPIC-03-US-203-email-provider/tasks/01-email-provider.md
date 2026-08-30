# Task 01 — Email Provider

**Story/AC:** US-203, original AC-11.5 / AC-203.1..3
**Layer:** Backend Application + Infrastructure
**Status:** not started

## Executable checklist

- [ ] Inspect `EmailMessageConsumer.cs`, `EmailMessage.cs`, external-API configuration/provider,
  `ISecretProtector`, and `ServiceCollectionExtensions.cs`; choose the existing settings seam.
- [ ] First add failing tests in `backend/tests/CustomerSupport.Tests/Unit/Services/EmailSenderTests.cs`:
  `SendAsync_UsesConfiguredProvider`, `SendAsync_TransientFailure_RetriesThreeTimesWithExponentialBackoff`,
  `SendAsync_TransientFailure_ExhaustsAttemptsAndThrowsSafeException`,
  `SendAsync_PermanentFailure_ThrowsWithoutRetry`, `SendAsync_MissingConfiguration_ThrowsWithoutProviderCall`,
  and `SendAsync_Cancellation_DoesNotRetry`.
- [ ] Add the Application contracts `IEmailSender.cs`, `EmailSendRequest.cs`, `EmailSendResult.cs`,
  and `EmailSendException.cs`; ensure no Infrastructure namespace is referenced.
- [ ] Add options, secret loading, SMTP adapter, transient classifier, deterministic delay seam, and
  bounded 1s/2s/4s retry in the exact production files named by the parent plan.
- [ ] Replace consumer stub behavior with delegation or remove duplicate retry logic; never claim
  delivery before provider acceptance.
- [ ] Add `EmailProviderWiringTests` methods `IEmailSender_IsRegisteredInInternalApi`,
  `EmailConsumer_DelegatesToSender`, and `MissingProviderConfiguration_IsNotReportedAsSuccess`.
- [ ] Run targeted tests, inspect output/logs for secrets and body text, then run the full backend
  build/test commands and paste actual output in story evidence.
- [ ] If schema changes, add/review migration and rollback notes before marking complete.

## Exact files

- New: `Application/Communication/{IEmailSender,EmailSendRequest,EmailSendResult,EmailSendException}.cs`.
- New: `Infrastructure/Email/{SmtpEmailSender,EmailProviderOptions,SmtpTransientFailureClassifier,EmailDelay}.cs`.
- Modify: `Infrastructure/Messaging/Consumers/EmailMessageConsumer.cs` and
  `Infrastructure/ServiceCollectionExtensions.cs`.
- New tests: `Unit/Services/EmailSenderTests.cs`, `Integration/EmailProviderWiringTests.cs`.

## Verification commands

```powershell
cd backend
dotnet test CustomerSupport.slnx --filter FullyQualifiedName~EmailProvider
dotnet test CustomerSupport.slnx
dotnet build CustomerSupport.slnx
```

## Status evidence

Record exact test names/counts, retry attempts/delays, configuration source, migration ID if any, and
sanitized logging review. No build or test command has been run while writing this plan.

## Deviation record

`None yet.` Record provider library, settings storage, classifier exceptions, or any test gap as a
fact with owner and follow-up.
