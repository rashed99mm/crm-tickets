# US-708 · Suggestion Outcome Tracking

| Field | Value |
|---|---|
| **Story** | `US-708` |
| **Epic** | [EPIC-11 AI Features](../epics/EPIC-11-ai.md) |
| **Feature** | [`FEAT-20` AI-Powered Features](../delivery-plan.md#feat-20--ai-powered-features) |
| **Layer** | Backend |
| **Ships with** | [US-703](./US-703-ai-human-gate.md) *(backend)* |
| **Actor** | System |
| **Priority** | P2 |
| **Sprint** | [15 — AI assist](../delivery-plan.md#sprint-15-ai-assist) · Slice S7 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-7.5 |
| **Spec criteria** | AC-708 |
| **Depends on** | [US-703](./US-703-ai-human-gate.md) |

## Story

**As a system**, **I want** to track whether AI suggestions are accepted, edited, or rejected, **so that** AI value is measurable.

## Business rules

- No BRD BR-n covers this directly. Every AI suggestion outcome is tracked with a status: `sent` (accepted as-is), `edited` (accepted with modifications), or `rejected`.
- No BRD BR-n covers this directly. Suggestion tracking data feeds into reporting for AI ROI analysis.

## Acceptance criteria

#### AC1 — Status transitions (spec AC-708)

Given an AI suggestion in `pending_approval`, when the agent acts on it, then the status is updated to `sent`, `edited`, or `rejected`.

#### AC2 — Edited flag tracked (spec AC-708)

Given an agent edits a draft reply before sending, when the suggestion status is updated, then `editedAt` is set and the original suggestion text is retained for comparison.

#### AC3 — Tracking queryable (spec AC-708)

Given suggestion outcomes are tracked, when an admin queries suggestion stats, then totals for sent/edited/rejected are returned.

## SQL tables

Extends `AiSuggestions` table from [US-703](./US-703-ai-human-gate.md).

```sql
-- Additional columns for tracking
ALTER TABLE [dbo].[AiSuggestions] ADD
    [EditedAt]           DATETIME2     NULL,
    [OriginalSuggestion] NVARCHAR(MAX) NULL,
    [SentAt]             DATETIME2     NULL;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-708 | Integration | `SuggestionStatusSent` | Given a pending suggestion, when the agent sends without edits, then status is `sent` and `SentAt` is set. | Status = sent, SentAt populated |
| TC-02 | AC-708 | Integration | `SuggestionStatusEdited` | Given a pending suggestion, when the agent edits and sends, then status is `edited` and `EditedAt` is set. | Status = edited, EditedAt populated |
| TC-03 | AC-708 | Integration | `SuggestionStatusRejected` | Given a pending suggestion, when the agent rejects, then status is `rejected`. | Status = rejected |
| TC-04 | AC-708 | Integration | `SuggestionStatsQueryable` | Given 10 suggestions (5 sent, 3 edited, 2 rejected), when stats are queried, then counts match. | Correct aggregate counts |

## Notes

This story extends the `AiSuggestions` table created in US-703. The tracking is passive — no agent action is required beyond the normal confirm/edit/reject flow.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
