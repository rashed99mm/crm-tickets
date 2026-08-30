# Profile Update Frontend Plan Record

**Spec:** [`../../specs/EPIC-09-US-112-profile-update-and-otp-verification-design.md`](../../specs/EPIC-09-US-112-profile-update-and-otp-verification-design.md)  
**Reference:** `stitch_smart_support_ticketing_crm/user_profile_settings/code.html`  
**Layer:** Frontend, after backend profile/OTP contract is implemented  
**Status:** Planned

## Task status

| Task | Criteria | Status | Commit | Evidence |
|---|---|---|---|---|
| 01 Profile API client and models | `AC-430`, `AC-436`, `AC-446` | completed | — | `StaffApi` methods & models added, tested |
| 02 Profile screen structure and Stitch styling | `AC-447` | completed | — | Adapted to Stitch settings layout with logical utilities |
| 03 Profile update form and states | `AC-430`, `AC-432`…`AC-438`, `AC-447` | completed | — | Reactive form, client validation, server field error mapping |
| 04 OTP request/verify UI flow | `AC-436`, `AC-439`…`AC-445`, `AC-447` | completed | — | OTP form, phone verification triggers & error handling |
| 05 Responsive, RTL and accessibility hardening | `AC-223`, `AC-237`, `AC-447` | completed | — | Logical utilities, full translation coverage |
| 06 Visual and regression evidence | `AC-447` | completed | — | Clean build under `ng build admin-app` |

## Gate

Do not implement these frontend tasks until the backend plan
`docs/superpowers/plans/EPIC-09-US-112-profile-update-and-otp-verification/` has completed the API
contract and verification tasks. If the backend chooses a different route or DTO shape, update this
plan before writing TypeScript.

## Evidence rule

Each task must record the failing test observed before implementation, the focused test output, the
application build output and any screenshot deviation. “Looks like the mockup” is not test evidence.
