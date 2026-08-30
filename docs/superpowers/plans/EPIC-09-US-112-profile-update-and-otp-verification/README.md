# Profile Update and OTP Verification plan record

**Spec:** [`../../specs/EPIC-09-US-112-profile-update-and-otp-verification-design.md`](../../specs/EPIC-09-US-112-profile-update-and-otp-verification-design.md)  
**Layer:** Backend first; frontend profile redesign follows the backend gate  
**Status:** Implemented

## Task status

| Task | Criteria | Status | Commit | Evidence |
|---|---|---|---|---|
| 01 Contract and current-user profile use case | `AC-430`–`AC-438`, `AC-446` | done | — | `CurrentUserProfileEndpointTests` (10 tests) |
| 02 Profile validation and Identity update boundary | `AC-432`–`AC-438` | done | — | validator + handler tests |
| 03 OTP verification domain and repository | `AC-439`–`AC-445` | done | — | `OtpVerificationDomainTests` |
| 04 OTP verify application handler | `AC-439`–`AC-444` | done | — | `VerifyOtpCommandHandlerTests` (6 tests) |
| 05 Verification route, authorization and contract tests | `AC-439`–`AC-446` | done | — | `OtpVerificationEndpointTests` (8 tests) |
| 06 Migration, security audit and backend evidence | `AC-432`–`AC-446` | done | — | migration `20260827134543_AddOtpVerification` (only `CodeHash`, `rowversion`) |

## Evidence (feature test run — 29 passed)

```
dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~CurrentUserProfile|FullyQualifiedName~OtpVerification|FullyQualifiedName~VerifyOtp"
Passed! - Failed: 0, Passed: 29, Skipped: 0, Total: 29
```

### Security evidence (AC-445)
- `OtpVerification` persists `CodeHash` only; no plaintext `Code`, no token, no `codeHash` in any response.
- `VerifyOtpResponse` carries only `Verified` + `Type`; the integration test asserts the raw JSON never
  contains the code (`123456`) or `codeHash`.
- `OtpCodeHasher` uses SHA-256 + server pepper with constant-time comparison; the pepper is configurable
  via `Otp:HashKey`.
- All unusable states (wrong/expired/locked/unknown/other-user) collapse to a single safe `OTP_INVALID`
  response, so the condition is never revealed (AC-440, AC-443).
- Verification write + Identity confirmation write share the scoped `AppDbContext` and commit in one
  `SaveChangesAsync`, so a failure cannot leave a half-confirmed contact (AC-444). The `rowversion`
  column makes a second concurrent success lose the race and fall back to idempotent success (AC-442).

## Frontend handoff

`AC-447` is intentionally a frontend criterion. After the backend tasks finish, write a frontend plan over
`profile.component.*`, `StaffApi`, and the relevant component tests before changing the profile UI.

## Deviations

- **Out of scope (per spec):** the OTP *request/generation/send* flow (`EPIC-09-US-112-otp-verification-design.md`,
  OTP-1..OTP-3) is a separate, dependent feature and was not built here. The verify endpoint operates on
  `OtpVerification` records created by that flow; integration tests seed records directly through the
  repository.
- **Test-project compile gate:** a full rebuild of `CustomerSupport.Tests` currently fails to compile due to
  pre-existing errors in files unrelated to this feature — a `Program` type ambiguity between the two API
  hosts (`CrmApiFactory`, `ChangePasswordEndpointTests`, `AutoEscalationEndpointTests`,
  `CrmExternalApiFactory`) and type-resolution errors in `MetaSignatureVerifierTests` /
  `WhatsAppNotificationChannelSenderTests` (Channels/WhatsApp, not part of this slice). These are not touched
  by this feature; the feature's own tests compile and pass in isolation. Flagged, not silently fixed, to
  avoid expanding scope into unrelated test infrastructure.
