# US-415 · Portal Satisfaction Survey Form

| Field | Value |
|---|---|
| **Story** | `US-415` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Frontend |
| **Ships with** | [US-409](./US-409-survey-endpoint.md) *(frontend)* |
| **Actor** | Customer |
| **Priority** | P1 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 2 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.7, FR-8.8 |
| **Spec criteria** | AC-15 |
| **Depends on** | [US-409](./US-409-survey-endpoint.md) |

## Story

**As a customer**, **I want** to rate resolved requests, **so that** feedback is captured.

## Business rules

- BR-23 — Rating must be an integer between 1 and 5 inclusive (BRD).

## Acceptance criteria

#### AC1 — Survey form submits rating via API (spec AC-15)

Given customer on resolved ticket detail, when survey submitted with rating 1-5, then POST /api/portal/tickets/{id}/survey is called and confirmation shown.

#### AC2 — Survey form validates rating range

Given no rating selected, when form submitted, then validation error displayed.

#### AC3 — Survey not shown if already submitted

Given ticket has existing survey response, when detail loads, then survey form is hidden and thank-you message shown.

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-15 | Component | `SurveyForm_RendersRatingSelector` | Given survey form loaded, when rendered, then 5-star rating or radio buttons visible | Rating input exists with 5 options |
| TC-02 | AC-15 | Component | `SurveyForm_ValidSubmit_CallsApi` | Given rating 4 selected, when submitted, then POST /api/portal/tickets/{id}/survey called | HTTP request fires with rating=4 |
| TC-03 | AC-2 | Component | `SurveyForm_NoRating_ShowsValidationError` | Given no rating selected, when submitted, then validation error displayed | No HTTP request fires |
| TC-04 | AC-3 | Component | `SurveyForm_AlreadySubmitted_HidesForm` | Given ticket has survey response, when component loads, then form not shown | Survey inputs absent from DOM |
| TC-05 | AC-3 | Component | `SurveyForm_AlreadySubmitted_ShowsThankYou` | Given ticket has survey response, when component loads, then thank-you message visible | Confirmation text displayed |

## Notes

FreeText field is optional. Form appears on resolved ticket detail screen (US-413) conditionally.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
