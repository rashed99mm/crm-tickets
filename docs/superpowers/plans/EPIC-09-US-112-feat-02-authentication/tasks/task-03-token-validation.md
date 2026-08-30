# Task 3 — Validate tokens and define role policies

> **Superseded 2026-08-25 by the platform baseline.** The backend this document describes was
> replaced when the CCE Platform reference was adopted as the CRM baseline — see
> [`EPIC-12-US-000-crm-platform-baseline-design.md`](../../../specs/EPIC-12-US-000-crm-platform-baseline-design.md).
> The code named below no longer exists in `src/`; it is archived, not deleted. This file is kept
> because it is the record of what was built and why, and deleting it would erase the reasoning
> behind decisions that still hold — the envelope, the localisation approach and the dependency rule
> among them. **Do not follow its steps.**


| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) |
| Feature | `FEAT-02` Authentication and session |
| Criteria | AC-3, part of AC-4 |
| Status | `done` |
| Commit | `9dbbc79` |

## Files

- `src/CustomerSupport.Api.Common/Common/ApiAuthenticationExtensions.cs`
- `src/CustomerSupport.Api.Common/Common/CustomerSupportApiComposition.cs`

## Test evidence

All 34 integration tests still pass; the health endpoint did not break.

Run and observed, not assumed. See the commit for the pasted suite output.

## Deviations from the plan

1. Narrowed from the plan, which called for registering validation on both hosts. In S1 every protected endpoint is on the admin host, and letting a customer-facing deployment accept a staff token buys nothing while widening the blast radius.
2. The pipeline detects whether an authentication scheme is registered rather than taking a flag, because UseAuthentication throws when none is.

## The point of this task

MapInboundClaims is off. The token already carries ClaimTypes.*; inbound remapping would rename them and break ICurrentUser and audit attribution without failing an obvious test.
