# US-704 · Summarise Ticket Thread

| Field | Value |
|---|---|
| **Story** | `US-704` |
| **Epic** | [EPIC-11 AI Features](../epics/EPIC-11-ai.md) |
| **Feature** | [`FEAT-20` AI-Powered Features](../delivery-plan.md#feat-20--ai-powered-features) |
| **Layer** | Backend |
| **Ships with** | [US-702](./US-702-ai-service-port.md) *(backend)* |
| **Actor** | Agent |
| **Priority** | P2 |
| **Sprint** | [15 — AI assist](../delivery-plan.md#sprint-15-ai-assist) · Slice S7 |
| **Estimate** | 5 points |
| **Status** | `done` |
| **BRD requirements** | FR-7.1 |
| **Spec criteria** | AC-704 |
| **Depends on** | [US-702](./US-702-ai-service-port.md), [US-703](./US-703-ai-human-gate.md) |

## Story

**As an agent**, **I want** AI-generated summaries of long ticket threads, **so that** I can quickly catch up on a ticket without reading every message.

## Business rules

- No BRD BR-n covers this directly. Thread summary is generated on-demand when the agent clicks "Summarise".
- No BRD BR-n covers this directly. Summary does not auto-save; it is displayed temporarily until the agent dismisses it.

## Acceptance criteria

#### AC1 — Summarise endpoint (spec AC-704)

Given a ticket with 3+ messages, when the agent requests a summary, then the endpoint returns a concise text summary of the thread.

#### AC2 — Summary displayed in UI (spec AC-704)

Given a summary is returned from the API, when the agent is on the ticket detail page, then the summary appears in a dedicated panel.

#### AC3 — Short threads not summarised (spec AC-704)

Given a ticket with fewer than 3 messages, when the agent requests a summary, then a message indicates the thread is too short to summarise.

#### AC4 — Sentiment chip (spec AC-21.11, 2026-08-28 amendment)

Given a summary is returned, when the agent views the Context Summary card, then the panel renders a sentiment chip (`Frustrated` / `Neutral` / `Satisfied`) alongside the summary text. A `null` sentiment renders no chip.

## SQL tables

None — backend story. Summaries are generated on-demand, not persisted.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-704 | Integration | `SummariseEndpointReturnsSummary` | Given a ticket with 5 messages, when the agent calls the summarise endpoint, then a non-empty summary string is returned. | Summary text returned |
| TC-02 | AC-704 | Component | `SummaryDisplaysInTicketDetail` | Given a summary is returned, when the ticket detail renders, then the summary panel shows the text. | Summary panel visible |
| TC-03 | AC-704 | Integration | `ShortThreadNotSummarised` | Given a ticket with 2 messages, when the agent requests a summary, then a "too short" message is returned. | "Too short" indicator |
| TC-04 | AC-21.11 | Integration | `SummarisePayloadIncludesSentiment` | Given a configured AI returns `Frustrated` for sentiment, when the agent calls the summarise endpoint, then the persisted `AiSuggestions.Payload` JSON has both `text` and `sentiment` fields and the returned DTO round-trips them. | Sentiment present |
| TC-05 | AC-21.11 | Integration | `SummariseSentimentFails_SummaryStillSucceeds` | Given the sentiment call fails, when the agent calls the summarise endpoint, then the response is `success=true` with `text` and `sentiment=null`. | Summary returned |

## Notes

Frontend: button on the ticket detail view triggers the summarise call. Backend: `IAiService.SummariseAsync` is called with the thread content. Uses `AgentsWorkspace.html` mockup as reference.

## Open questions

None.

## Status evidence

Implemented 2026-08-28.

- Backend: `SummariseTicketCommandHandler` (`backend/src/CustomerSupport.Application/Features/Ai/AiFeatures.cs:67-120`) writes `{ text, sentiment }` to `AiSuggestions.Payload`. `IAiService.ClassifySentimentAsync` added in `backend/src/CustomerSupport.Application/Ai/IAiService.cs`. Provider impl in `backend/src/CustomerSupport.Infrastructure/Ai/ResilientAiService.cs`. `NoOpAiService.ClassifySentimentAsync` returns `Fail` (sentiment is `null` when the deployment is unconfigured).
- Frontend: `ai-panel.component` (`frontend/projects/admin-app/src/app/features/tickets/ai-panel.component.{ts,html}`) renders the Context Summary card with the sentiment chip.
- Spec: `docs/superpowers/specs/EPIC-11-US-701-feat-21-stitch-right-rail.md` (AC-21.11, AC-F9).
- Status reflects committed and executed code, never intent.
