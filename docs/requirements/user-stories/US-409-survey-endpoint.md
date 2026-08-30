# US-409 · Survey Submission Endpoint

| Field | Value |
|---|---|
| **Story** | `US-409` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Backend |
| **Ships with** | — |
| **Actor** | Customer |
| **Priority** | P1 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.7, FR-8.8, BR-20 |
| **Spec criteria** | AC-9 |
| **Depends on** | [US-401](./US-401-customer-registration.md), [US-403](./US-403-customer-authorization.md), [US-404](./US-404-portal-submit-ticket.md), [US-407](./US-407-portal-reply.md), [US-408](./US-408-survey-response-entity.md) |

## Story

**As a customer**, **I want** to rate resolved requests, **so that** feedback is captured.

## Business rules

- BR-20 — Customer scoped to own records (BRD).
- BR-23 — Rating must be an integer between 1 and 5 inclusive (BRD).
- BR-24 — One survey response per resolved ticket (BRD).

## Acceptance criteria

#### AC1 — Customer submits survey for own resolved ticket (spec AC-9)

Given customer authenticated and ticket is resolved, when survey submitted, then SurveyResponse is created linked to the ticket.

## SQL tables

`SurveyResponses` — see [US-408](./US-408-survey-response-entity.md).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-9 | Integration | `SubmitSurvey_ValidRating_Created` | Given customer owns resolved ticket, when POST /api/portal/tickets/{id}/survey with rating 5, then 201 | SurveyResponse persisted with correct TicketId and Rating |
| TC-02 | AC-9 | Integration | `SubmitSurvey_UnresolvedTicket_Returns400` | Given open ticket, when survey submitted, then 400 Bad Request | Error indicates ticket must be resolved |
| TC-03 | AC-9 | Integration | `SubmitSurvey_DuplicateSubmission_Returns409` | Given survey already submitted for ticket, when submitting again, then 409 Conflict | Error indicates survey already submitted |
| TC-04 | AC-9 | Integration | `SubmitSurvey_OtherCustomerTicket_Returns403` | Given customer A, when submitting survey for customer B's ticket, then 403 | Authorization error returned |
| TC-05 | AC-9 | Integration | `SubmitSurvey_InvalidRating_Returns400` | Given rating 0 or 6, when submitted, then 400 Bad Request | Validation error for rating range |

## Notes

Endpoint checks ticket ownership, ticket status is Resolved/Closed, and no existing survey before creating.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
