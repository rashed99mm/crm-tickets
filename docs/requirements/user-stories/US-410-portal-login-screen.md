# US-410 · Portal Login Screen

| Field | Value |
|---|---|
| **Story** | `US-410` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Frontend |
| **Ships with** | [US-402](./US-402-customer-login.md) *(frontend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.1 |
| **Spec criteria** | AC-10 |
| **Depends on** | [US-402](./US-402-customer-login.md) |

## Story

**As a customer**, **I want** a login screen, **so that** I can access the portal.

## Business rules

None.

## Acceptance criteria

#### AC1 — Login screen authenticates and redirects (spec AC-10)

Given customer on login screen, when valid credentials submitted, then JWT is stored and customer is redirected to my-tickets.

#### AC2 — Login screen displays validation errors

Given invalid credentials, when login submitted, then error message is displayed without redirect.

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-10 | Component | `LoginScreen_RendersEmailAndPasswordFields` | Given login screen loaded, when rendered, then email and password inputs visible | Both inputs exist in DOM |
| TC-02 | AC-10 | Component | `LoginScreen_ValidSubmit_CallsApi` | Given valid credentials, when form submitted, then POST /api/portal/login called with correct payload | HTTP request fires |
| TC-03 | AC-10 | Component | `LoginScreen_Success_RedirectsToMyTickets` | Given successful login, when response received, then router navigates to /portal/tickets | URL changes to /portal/tickets |
| TC-04 | AC-2 | Component | `LoginScreen_InvalidCredentials_ShowsError` | Given wrong password, when login submitted, then error message displayed | Error text visible in UI |

## Notes

Login screen is part of the portal-app Angular application. Uses Angular signals for form state.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
