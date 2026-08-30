# US-414 · Portal Reply Form

| Field | Value |
|---|---|
| **Story** | `US-414` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Frontend |
| **Ships with** | [US-407](./US-407-portal-reply.md) *(frontend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 2 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.5 |
| **Spec criteria** | AC-14 |
| **Depends on** | [US-407](./US-407-portal-reply.md) |

## Story

**As a customer**, **I want** to reply to my agent, **so that** I can provide information.

## Business rules

None.

## Acceptance criteria

#### AC1 — Reply form posts message via API (spec AC-14)

Given customer on reply form, when content submitted, then POST /api/portal/tickets/{id}/reply is called and new message appears in history.

#### AC2 — Reply form validates non-empty content

Given empty content, when form submitted, then validation error displayed and no API call made.

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-14 | Component | `ReplyForm_RendersTextAreaAndButton` | Given reply form loaded, when rendered, then textarea and submit button visible | Both elements exist in DOM |
| TC-02 | AC-14 | Component | `ReplyForm_ValidSubmit_CallsApi` | Given non-empty content, when submitted, then POST /api/portal/tickets/{id}/reply called | HTTP request fires with correct body |
| TC-03 | AC-2 | Component | `ReplyForm_EmptyContent_ShowsValidationError` | Given empty textarea, when submitted, then validation error displayed | No HTTP request fires |
| TC-04 | AC-14 | Component | `ReplyForm_Success_ClearsAndRefreshes` | Given successful reply, when response received, then textarea cleared and message history refreshed | Input is empty after success |

## Notes

Component embedded in ticket detail screen (US-413). Uses reactive forms with signals.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
