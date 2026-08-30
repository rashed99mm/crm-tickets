# US-705 · Suggest Category + Priority

| Field | Value |
|---|---|
| **Story** | `US-705` |
| **Epic** | [EPIC-11 AI Features](../epics/EPIC-11-ai.md) |
| **Feature** | [`FEAT-20` AI-Powered Features](../delivery-plan.md#feat-20--ai-powered-features) |
| **Layer** | Backend |
| **Ships with** | [US-702](./US-702-ai-service-port.md) *(backend)* |
| **Actor** | Agent |
| **Priority** | P2 |
| **Sprint** | [15 — AI assist](../delivery-plan.md#sprint-15-ai-assist) · Slice S7 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-7.2 |
| **Spec criteria** | AC-705 |
| **Depends on** | [US-702](./US-702-ai-service-port.md), [US-703](./US-703-ai-human-gate.md) |

## Story

**As an agent**, **I want** AI suggestions for ticket category and priority, **so that** triage is faster and more consistent.

## Business rules

- No BRD BR-n covers this directly. Category and priority suggestions are based on the ticket subject and first message content.
- No BRD BR-n covers this directly. Suggestions are offered as clickable options, not auto-applied.

## Acceptance criteria

#### AC1 — Suggest endpoint (spec AC-705)

Given a new ticket with subject and body, when the agent requests category/priority suggestions, then the endpoint returns suggested category and priority values.

#### AC2 — Suggestions displayed as options (spec AC-705)

Given suggestions are returned, when the agent views the ticket, then suggested category and priority appear as clickable options the agent can accept or ignore.

#### AC3 — Agent accepts suggestion (spec AC-705)

Given a suggested category, when the agent clicks to accept it, then the ticket's category is updated to the suggested value.

## SQL tables

Suggestions stored in `AiSuggestions` table (defined in [US-703](./US-703-ai-human-gate.md)).

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-705 | Integration | `SuggestCategoryEndpoint` | Given a ticket with "password reset" subject, when the agent requests suggestions, then "Account Access" or similar category is suggested. | Relevant category suggested |
| TC-02 | AC-705 | Component | `SuggestionDisplaysAsClickable` | Given suggestions are returned, when the ticket detail renders, then category suggestion appears as a button/chip. | Clickable suggestion UI |
| TC-03 | AC-705 | Integration | `AcceptSuggestionUpdatesTicket` | Given a suggested category, when the agent accepts, then `Tickets.Category` is updated to the suggested value. | Ticket category updated |

## Notes

Frontend: suggestion chips on the ticket detail view. Backend: `IAiService.SuggestCategoryAsync` uses the ticket content. Uses `AgentsWorkspace.html` mockup as reference.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
