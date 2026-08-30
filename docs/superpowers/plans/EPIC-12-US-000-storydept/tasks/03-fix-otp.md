# Task 03 — Fix OTP handler semantics (×3)

## Traceability
Epic:   docs/requirements/epics/EPIC-07-customer-portal.md
Stories: none exist yet — FIRST STEP of this task: file
         docs/requirements/user-stories/US-416-otp-verification.md (cooldown, safe failure,
         nothing persisted on refusal) so the scope is not silent.
FEAT:   (unassigned) — sprint 9 neighbourhood
Spec:   docs/superpowers/specs/EPIC-09-US-112-otp-verification-design.md
Plan:   docs/superpowers/plans/EPIC-09-US-112-otp-verification/

## Work
RequestOtpCommandHandler.cs (~line 32) — three failing tests:
- WithinCooldown_RefusesWithoutGeneratingOrSending
- GatewayThrows_SafeFailureAndNothingPersisted
- GatewayRefusal_PersistsNothingAndReturnsSafeFailure
Fix the handler so a cooldown refusal and a gateway failure persist nothing and return the safe
failure shape; success still generates + sends.

## Gate
dotnet test --filter "FullyQualifiedName~RequestOtpCommandHandlerTests" → green, output pasted.
