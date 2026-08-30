# US-918 · KB Admin: Kill confirm + Hardcoded English

| Field | Value |
|---|---|
| **Story** | `US-918` |
| **Epic** | [EPIC-14 Phase 2 BI & Workflow](../epics/EPIC-14-phase2-bi-and-workflow.md) |
| **Feature** | [`FEAT-30`](../delivery-plan.md#feat-30) |
| **Layer** | Frontend |
| **Ships with** | (none) |
| **Actor** | ContentManager, Admin |
| **Priority** | P1 |
| **Sprint** | 19 — UX redesign |
| **Estimate** | 3 points |
| **Status** | `not started` |

## Story

**As a content manager**, **I want** the KB admin to behave like the rest of the app, **so that**
archive is a styled, translatable confirmation, not a browser dialog in hardcoded English.

## Business rules

- `window.confirm('Archive this article?')` → inline confirm
  (same pattern as customer delete, which deliberately avoids native dialogs).
- Hardcoded strings (`'Body is required.'`, `'Title is required.'`, `'Something went wrong.'`)
  route through `| t`.
- Form moves to reactive forms if feasible within the story (or at minimum keeps working while the
  strings are translated — follow what the plan decides).

## Acceptance criteria

#### AC1 — No confirm/hardcoded strings

Given KB admin, then archive uses an inline confirm and every visible string is translated; error
fallbacks are not hardcoded English.

#### AC2 — i18n + RTL safe

Given the redesigned KB screens, then all strings translate and layout is RTL-safe.

## Test cases

| # | Criterion | Level | Test | Expected |
|---|---|---|---|---|
| TC-01 | AC1 | Component | `KbAdmin_Archive_InlineConfirm` | no `window.confirm` call |
| TC-02 | AC1 | Component | `KbAdmin_Errors_Translated` | `| t` keys in template |

## SQL tables

None.

## Notes

The KB admin screen and its translations are reworked.

## Status evidence

Not yet shipped.

Status is set from what is committed and executed, never from what is planned.