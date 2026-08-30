# US-605 · CSAT Report

| Field | Value |
|---|---|
| **Story** | `US-605` |
| **Epic** | [EPIC-08 Reports & Management](../epics/EPIC-08-reporting.md) |
| **Feature** | [`FEAT-19` Reporting](../delivery-plan.md#feat-19--reporting) |
| **Layer** | Backend |
| **Ships with** | [US-601](./US-601-reports-controller.md) *(Backend)* |
| **Actor** | Manager |
| **Priority** | P0 |
| **Sprint** | [13 — Reporting](../delivery-plan.md#sprint-13-reporting) · Slice S6 |
| **Estimate** | 3 points |
| **Status** | `implemented` — backend endpoint + frontend report card wired; tests skipped this pass |
| **BRD requirements** | FR-9.4 |
| **Spec criteria** | AC-605 |
| **Depends on** | [US-601](./US-601-reports-controller.md) *(Backend)*, [US-608](./EPIC-08-US-608-report-scoping.md) *(Backend)* |

## Story

**As a manager**, **I want** customer satisfaction ratings broken down by language and channel, **so that** I ensure equal quality of service across all customer segments.

## Business rules

- No BRD BR-n covers this directly. CSAT report groups ratings by language and channel, showing average score and response rate.
- BR-21: Report results are branch-scoped by default; the caller's branch is enforced via JWT claims.

## Acceptance criteria

#### AC1 — CSAT by language (spec AC-605)

Given tickets with CSAT ratings and language metadata, when the manager requests the CSAT report, then satisfaction scores are grouped by customer language.

#### AC2 — CSAT by channel (spec AC-605)

Given tickets with CSAT ratings and channel metadata, when the manager requests the CSAT report, then satisfaction scores are grouped by channel (email, web, phone).

## SQL tables

None — read-only query over existing tables.

```sql
SELECT t.language, t.channel,
       AVG(cs.rating) AS avgRating,
       COUNT(cs.id) AS totalResponses,
       COUNT(t.id) AS totalTickets
FROM Tickets t
LEFT JOIN CustomerSatisfaction cs ON cs.ticketId = t.id
WHERE t.resolvedAt BETWEEN @startDate AND @endDate
GROUP BY t.language, t.channel;
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-605 | Integration | `CsatReportByLanguage` | Given English tickets avg 4.2 and French tickets avg 3.8, when the manager requests CSAT, then both language groups appear with correct averages. | Correct language grouping |
| TC-02 | AC-605 | Integration | `CsatReportByChannel` | Given email channel avg 4.0 and web channel avg 4.5, when the manager requests CSAT, then both channel groups appear. | Correct channel grouping |

## Notes

CSAT ratings are collected post-resolution. Tickets without ratings are excluded from the average but counted in the total.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
