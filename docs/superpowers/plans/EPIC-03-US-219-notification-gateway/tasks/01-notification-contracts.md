# Task 01 — Notification Contracts

**Story:** `US-201`, `US-203`, `US-204`, `US-205`, `US-219`  
**Criteria:** `NG-1`, `NG-2`, `NG-7`

## Files

- Add `backend/src/CustomerSupport.Application/Notifications/NotificationDispatchRequest.cs`.
- Add `backend/src/CustomerSupport.Application/Notifications/INotificationGateway.cs`.
- Add channel result records and `INotificationChannelSender`.
- Update `ApplicationErrors`, `SystemCode`, `SystemCodeMap`, and `Resources.yaml`.

## Steps

1. Write failing contract tests for channel selection, missing provider configuration, and stable
   error-code resolution.
2. Add provider-neutral records; Application must not reference Infrastructure or HTTP types.
3. Add localized domain keys and verify every key maps to one system code.
4. Add redacted result types that contain provider IDs only, never provider response bodies.

**Run:** `dotnet test backend/CustomerSupport.slnx --filter "FullyQualifiedName~NotificationContract"`  
**Expected:** Contract tests pass after implementation; no duplicate system codes.  
**Commit:** `feat: define notification gateway contracts`

## Security

Reject empty recipient/channel/template input before provider dispatch. Do not serialize variables
that contain secrets or verification codes.

## Evidence

Record the actual command output and any deviation before updating story status.
