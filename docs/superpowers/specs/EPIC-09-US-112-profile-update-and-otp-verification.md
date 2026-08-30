# Profile Update and OTP Contact Verification

**Date:** 2026-08-27  
**Status:** Draft for approval  
**Type:** Backend feature with a dependent frontend profile redesign  
**Related:** `EPIC-09-US-112-otp-verification-design.md`, `EPIC-03-US-219-notification-gateway.md`  
**Reference UI:** `stitch_smart_support_ticketing_crm/user_profile_settings/code.html`

## Problem

An authenticated user can view the current profile and change their password, but cannot safely
update the profile fields represented by the supplied settings design. The existing generic user
update route is Admin-only and accepts a path user id, which is not the correct boundary for a
self-service profile. Email and phone contact changes also need verification before their Identity
confirmation state is changed.

## Assumptions

- **A1.** The profile settings screen is a self-service staff profile for the authenticated user;
  administrator editing of another account remains the existing `/api/Users/{id}` capability.
- **A2.** The first implementation persists the fields already present in `ApplicationUser`:
  `FirstName`, `LastName`, `PhoneNumber` and `ProfileImageUrl`. `JobTitle` and `TimeZone` are shown
  by the Stitch reference but have no current domain columns, so they are not silently added here.
- **A3.** Email is read-only in the first profile update flow, matching the reference text that an
  administrator must change it. Email verification is still supported by the reusable OTP flow for
  registration/recovery or a future authorized email-change command.
- **A4.** Phone changes are stored as unconfirmed until the supplied phone OTP is verified. The
  existing phone confirmation flag is the Identity source of truth.
- **A5.** OTP request generation, channel dispatch, hashing, cooldown and rate limiting follow the
  approved `EPIC-09-US-112-otp-verification-design.md`; this feature specifies the integration point and
  verify behavior without duplicating those rules.
- **A6.** Profile update returns the same `UserInfoDto` shape as `GET /api/Auth/me`, so the frontend
  can update its session-facing profile without receiving tokens or Identity internals.
- **A7.** All request validation is server-side, all timestamps are UTC, and all messages use the
  existing bilingual response envelope.

## Out of scope

- Google OAuth. The pasted Google authorization URL is an external authorization-code flow and is
  not required to update the local CRM profile or verify a CRM OTP.
- Email address change, password change policy, avatar file upload/storage and social-provider
  profile synchronization.
- Persisting `JobTitle` or `TimeZone` until a separate domain/schema decision is approved.
- Returning a plaintext OTP, storing it, logging it, or including it in a response.
- Allowing a client-supplied user id to select the authenticated profile.

## Acceptance criteria

### Self-service profile update

- **AC-430.** Given an authenticated user, when they call `PUT /api/Auth/me` with valid
  `firstName`, `lastName`, optional `phoneNumber` and optional `profileImageUrl`, then only their
  own `ApplicationUser` is updated and the response is `200` with the updated `UserInfoDto`.
- **AC-431.** Given an unauthenticated caller, when they call `PUT /api/Auth/me`, then the response
  is `401` and no user row changes.
- **AC-432.** Given a valid profile update request, when it is processed, then no role, email,
  username, active state, password, department or branch value can be changed through binding or
  handler logic.
- **AC-433.** Given a first or last name that is empty, whitespace-only, or longer than 100
  characters, when the profile is updated, then the response is `400` with a field-keyed validation
  error and no profile fields are changed.
- **AC-434.** Given a phone number that is not null and fails the approved normalized phone format,
  when the profile is updated, then the response is `400` with a `phoneNumber` field error and no
  profile fields are changed.
- **AC-435.** Given a profile image URL that is not null and is not an allowed absolute `https` URL
  within the configured length limit, when the profile is updated, then the response is `400` with
  a `profileImageUrl` field error and no profile fields are changed.
- **AC-436.** Given a valid update with a changed phone number, when it succeeds, then the phone is
  saved as unconfirmed and the response exposes the new phone plus its current confirmation state;
  it does not claim verification occurred.
- **AC-437.** Given a valid update with the same phone number, when it succeeds, then the existing
  phone confirmation state is preserved and no unnecessary verification reset occurs.
- **AC-438.** Given an unknown or inactive authenticated user context, when the profile is updated,
  then the response is `404` or the approved inactive-account failure and no other account is touched.

### OTP verification

- **AC-439.** Given an OTP verification record with a valid unexpired hash and linked contact, when
  the caller submits the correct six-digit code to `POST /api/verification/verify`, then exactly one
  verification is marked complete and the linked email or phone confirmation flag is updated.
- **AC-440.** Given an incorrect, malformed, expired, invalidated or already verified code, when it
  is submitted, then the response is the same safe verification failure, no contact confirmation is
  changed, and the response never reveals which invalid condition occurred.
- **AC-441.** Given an OTP record with five failed attempts, when another code is submitted, then
  the record remains locked, no hash comparison is performed, and no account state changes.
- **AC-442.** Given two concurrent requests submit the correct code for the same OTP record, when
  both complete, then at most one returns success and the final record and Identity confirmation
  state are consistent.
- **AC-443.** Given a verification id belonging to another user or an unknown verification id, when
  an authenticated verification request is made, then the response is a safe `404`/verification
  failure without confirming record existence.
- **AC-444.** Given the verification service encounters a persistence or Identity update failure,
  when verification is attempted, then the operation is atomic or rolled back, the response uses
  the standard safe error envelope, and no partially confirmed contact remains.
- **AC-445.** Given any profile or OTP request, when logs, response bodies and persisted records are
  inspected, then passwords, access tokens, refresh tokens and plaintext OTP values are absent.

### Contract and frontend design dependency

- **AC-446.** Given `GET /api/Auth/me` or a successful `PUT /api/Auth/me`, when the response is
  serialized, then it uses the existing envelope and a documented camel-case `UserInfoDto` without
  access/refresh tokens or writable Identity fields.
- **AC-447.** Given the profile frontend is implemented after this backend slice, when it renders the
  Stitch `user_profile_settings` design, then it uses the profile DTO for identity/personal data,
  keeps email read-only, marks phone as unconfirmed until OTP success, and renders unsupported
  `JobTitle`/`TimeZone` values as explicit non-editable unavailable states rather than invented data.

## Design

### Application

Add a self-service `UpdateCurrentUserProfileCommand` under
`backend/src/CustomerSupport.Application/Features/Auth/Commands/UpdateCurrentUserProfile/` and a
validator for the request DTO. It reads `IUserContext.UserId`; it never accepts a user id. Reuse
`IIdentityUserService` for lookup/update and add only the minimum identity adapter method needed to
return the updated projection or confirmation state.

Add `VerifyOtpCommand` under
`backend/src/CustomerSupport.Application/Features/Verification/Commands/VerifyOtp/`, using an
application port for the OTP repository and an identity-confirmation port. The handler owns
authorization, safe failure behavior and the transaction boundary; Infrastructure owns EF and
Identity implementation.

### API

- `GET /api/Auth/me` remains the profile read endpoint.
- `PUT /api/Auth/me` accepts `{ firstName, lastName, phoneNumber, profileImageUrl }` and returns
  `Response<UserInfoDto>`.
- `POST /api/verification/verify` accepts `{ verificationId, code }` and returns a minimal
  `VerifyOtpResponse` containing verification status and contact type, never the code.
- Existing Admin `PUT /api/Users/{id}` remains separate and must not be weakened.

### Persistence and concurrency

`OtpVerification` stores a one-way code hash, expiry, failed-attempt count, lock/verified state,
contact, type, user linkage and a concurrency token. Verification compares a hash of the supplied
code, increments failures transactionally, and updates Identity confirmation in the same unit of
work. A second successful concurrent request must lose the concurrency race or observe the verified
state.

### Frontend design handoff

The backend slice must finish before a separate frontend plan is written. That frontend plan will
target `frontend/projects/admin-app/src/app/features/account/profile.component.{ts,html}` and its
spec, using the exact layout from
`stitch_smart_support_ticketing_crm/user_profile_settings/code.html`, while preserving typed forms,
`StaffApi`, `LocaleStore`, `CsInputField`, RTL logical utilities and the existing password section.
