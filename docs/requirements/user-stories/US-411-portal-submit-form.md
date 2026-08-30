# US-411 · Portal Ticket Submission Form

| Field | Value |
|---|---|
| **Story** | `US-411` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Frontend |
| **Ships with** | [US-404](./US-404-portal-submit-ticket.md) *(frontend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.2 |
| **Spec criteria** | AC-11 |
| **Depends on** | [US-404](./US-404-portal-submit-ticket.md) |

## Story

**As a customer**, **I want** to submit requests through a form, **so that** my issues are captured.

## Business rules

None.

## Acceptance criteria

#### AC1 — Form submits ticket via API (spec AC-11)

Given customer on ticket form, when valid data submitted, then POST /api/portal/tickets is called and customer sees confirmation.

#### AC2 — Form validates required fields

Given empty subject, when form submitted, then validation error displayed and no API call made.

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-11 | Component | `SubmitForm_RendersSubjectAndDescriptionFields` | Given form loaded, when rendered, then subject and description inputs visible | Both inputs exist in DOM |
| TC-02 | AC-11 | Component | `SubmitForm_ValidSubmit_CallsApi` | Given valid subject and description, when form submitted, then POST /api/portal/tickets called | HTTP request fires with correct body |
| TC-03 | AC-2 | Component | `SubmitForm_EmptySubject_ShowsValidationError` | Given empty subject, when submitted, then validation error displayed | No HTTP request fires |
| TC-04 | AC-11 | Component | `SubmitForm_Success_ShowsConfirmation` | Given successful submission, when response received, then success message displayed | Confirmation text visible in UI |

## Notes

Uses reactive forms with signals. Subject is required; description is optional.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
