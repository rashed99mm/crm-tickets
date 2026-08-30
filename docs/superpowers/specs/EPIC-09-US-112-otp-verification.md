# OTP Verification Through Notification Channels

**Epic:** `EPIC-09 Security & Administration`  
**Sprint:** `9 — Notification Gateway and Communication Channels`  
**Feature:** `FEAT-15` security capability  
**Dependency:** [`EPIC-03-US-219-notification-gateway.md`](./EPIC-03-US-219-notification-gateway.md)

## Problem

The platform needs a reusable verification flow for email and phone contacts without embedding
provider calls in authentication or portal features. Verification codes must be short-lived,
single-purpose, rate-limited, and delivered through the same configured channel integrations used
by the rest of the product.

## Assumptions

- **A1:** Email and SMS are both available through `INotificationGateway` and the configured
  `EmailGateway`/`SmsGateway` integration URLs.
- **A2:** The existing Identity user is updated only after successful verification; anonymous
  requests are allowed where the consuming registration or recovery flow requires them.
- **A3:** OTP values are generated with a cryptographically secure random source and stored only as
  a one-way hash.
- **A4:** All timestamps are UTC and all response messages are localized through the existing
  message catalogue.

## Out of scope

- Password reset policy and customer registration screens themselves.
- Returning the OTP code in an API response, test fixture response, log, or database field.
- WhatsApp or push verification.

## Acceptance criteria

- **OTP-1:** Given a valid email contact, when verification is requested, then a six-digit code is
  hashed, persisted with a five-minute expiry, and dispatched through the Email channel.
- **OTP-2:** Given a valid phone contact, when verification is requested, then a six-digit code is
  hashed, persisted with a five-minute expiry, and dispatched through the SMS channel.
- **OTP-3:** Given a recent request for the same contact and channel, when another request arrives
  within 60 seconds, then it is refused without sending another message.
- **OTP-4:** Given a correct unexpired code, when it is verified, then the verification is marked
  complete and the linked Identity email or phone confirmation flag is updated.
- **OTP-5:** Given an incorrect, expired, invalidated, or exhausted verification, when a code is
  submitted, then verification fails with a stable safe error and no account state changes.
- **OTP-6:** Given five failed attempts, when another code is submitted, then the record is locked
  and no further comparison or notification occurs.
- **OTP-7:** Given an unknown contact, when a request is made, then the response does not reveal
  whether an account exists and the channel behavior follows the approved public-flow policy.
- **OTP-8:** Given concurrent verification requests, when the same record is verified twice, then at
  most one request can mark it verified and confirmation updates remain consistent.
- **OTP-9:** Given a provider timeout or unavailable integration, when an OTP is requested, then the
  standard safe failure envelope is returned and no plaintext code is persisted.

## Design

The Application layer owns `OtpVerificationType`, commands, validators, DTOs, and the
`IOtpCodeGenerator` port. The gateway owns delivery. Infrastructure owns the code generator,
repository/database mapping, and Identity confirmation update. Public routes are hosted by
`CustomerSupport.ExternalApi` and are rate-limited independently from authenticated admin routes.

The request flow persists the hashed record only after the gateway accepts the dispatch. The verify
flow increments failed attempts transactionally and uses a concurrency token or serialized update
so two successful requests cannot both win.
