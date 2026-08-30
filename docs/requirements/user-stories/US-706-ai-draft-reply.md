# US-706 · Draft Suggested Reply

| Field | Value |
|---|---|
| **Story** | `US-706` |
| **Epic** | [EPIC-11 AI Features](../epics/EPIC-11-ai.md) |
| **Feature** | [`FEAT-20` AI-Powered Features](../delivery-plan.md#feat-20--ai-powered-features) |
| **Layer** | Backend |
| **Ships with** | [US-702](./US-702-ai-service-port.md) *(backend)* |
| **Actor** | Agent |
| **Priority** | P2 |
| **Sprint** | [15 — AI assist](../delivery-plan.md#sprint-15-ai-assist) · Slice S7 |
| **Estimate** | 8 points |
| **Status** | `done` |
| **BRD requirements** | FR-7.3 |
| **Spec criteria** | AC-706 |
| **Depends on** | [US-702](./US-702-ai-service-port.md), [US-703](./US-703-ai-human-gate.md) |

## Story

**As an agent**, **I want** AI-drafted reply suggestions, **so that** I can respond to customers faster while maintaining quality.

## Business rules

- No BRD BR-n covers this directly. Draft replies are based on the full thread context, knowledge base articles, and the agent's tone settings.
- BR-19 — Draft replies are drafts only; they require human confirmation before sending (BRD FR-7.6).

## Acceptance criteria

#### AC1 — Draft reply endpoint (spec AC-706)

Given a ticket with messages, when the agent requests a draft reply, then the endpoint returns an array of suggested reply strings (1 to 3 entries) — the 2026-08-28 amendment replaces the singular "a suggested response text" with a list so the right rail can render multiple candidates and the composer toolbar can use the first.

#### AC2 — Draft displayed in composer (spec AC-706)

Given a draft reply is returned, when the agent is composing a response, then the first draft is pre-filled into the reply composer for review.

#### AC3 — Agent can edit before send (spec AC-706)

Given a draft reply is pre-filled, when the agent edits and sends it, then the edited version is sent to the customer.

#### AC4 — Draft is pending approval (spec AC-706)

Given a draft reply is generated, when saved, then status is `pending_approval` in `AiSuggestions`.

#### AC5 — Insert from card (spec AC-21.12, AC-F10, 2026-08-28 amendment)

Given the Suggested Replies card lists N drafts, when the agent clicks Insert on row N, then the Nth draft is written into the composer body. The existing `recordMessage` flow is unchanged.

## SQL tables

Draft replies stored in `AiSuggestions` table (defined in [US-703](./US-703-ai-human-gate.md)).

```sql
-- AiSuggestions record for draft reply
INSERT INTO [dbo].[AiSuggestions] ([TicketId], [Type], [Suggestion], [Status], [CreatedByAgentId])
VALUES (@ticketId, 'reply', @draftsJson, 'pending_approval', @agentId);
-- draftsJson is a JSON document shaped { "drafts": [string, string, string] }
-- 2026-08-28 amendment: replaces the singular @draftText.
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-706 | Integration | `DraftReplyEndpointReturnsDrafts` | Given a ticket with 3 messages, when the agent requests a draft, then the response payload `drafts` is an array of length 1 to 3 with non-empty strings. | Drafts array returned |
| TC-02 | AC-706 | Component | `FirstDraftPreFilledInComposer` | Given the drafts array is returned, when the agent clicks the composer toolbar's Draft with AI, then the body text area contains `drafts[0]`. | Composer pre-filled |
| TC-03 | AC-706 | Integration | `DraftReplySavedAsPending` | Given a draft is generated, when saved, then `AiSuggestions.Status` is `pending_approval` and `Payload` is a `drafts` array. | Status = pending_approval |
| TC-04 | AC-21.12 | Component | `InsertFromCardWritesToComposer` | Given the Suggested Replies card lists N drafts, when the agent clicks Insert on row N, then the composer body contains the Nth draft. | Insert writes body |

## Notes

This is the highest-value AI feature. The draft quality depends on thread context and KB articles. Frontend: "AI Draft" button in the reply composer area. Uses `AgentsWorkspace.html` mockup as reference.

## Open questions

None.

## Status evidence

Implemented 2026-08-28.

- Backend: `DraftReplyCommandHandler` (`backend/src/CustomerSupport.Application/Features/Ai/AiFeatures.cs:181-228`) persists `{ drafts: [...] }`. `ResilientAiService.DraftReplyAsync` asks the model for three drafts in one call (`backend/src/CustomerSupport.Infrastructure/Ai/ResilientAiService.cs:46-55`) and re-uses the existing `AiJson.ParseStringArray` schema.
- Frontend: `TicketMessagesComponent.insertDraft()` and Draft with AI toolbar button in `frontend/projects/admin-app/src/app/features/tickets/ticket-messages.component.{ts,html}`; the Suggested Replies card's Insert buttons in `ai-panel.component.{ts,html}`.
- Spec: `docs/superpowers/specs/EPIC-11-US-701-feat-21-stitch-right-rail.md` (AC-21.12, AC-F10, AC-F13, AC-F14).
- Status reflects committed and executed code, never intent.
