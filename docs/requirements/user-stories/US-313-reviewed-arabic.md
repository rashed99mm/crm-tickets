# US-313 · Reviewed Arabic Translations

| Field | Value |
|---|---|
| **Story** | `US-313` |
| **Epic** | [EPIC-04 Agent dashboard](../epics/EPIC-04.md) |
| **Feature** | [`FEAT-17` Localisation & branding](../delivery-plan.md#feat-17--localisation-branding) |
| **Layer** | Frontend |
| **Ships with** | — |
| **Actor** | User |
| **Priority** | P1 |
| **Sprint** | [14 — Localisation and branding](../delivery-plan.md#sprint-14-localisation-and-branding) · Slice S14 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-12.5 |
| **Spec criteria** | AC-24 |
| **Depends on** | — |

## Story

**As an Arabic user**, **I want** accurate translations, **so that** I understand the interface.

## Business rules

- No BRD BR-n covers this directly. Reviewed Arabic translations, not placeholder or machine-translated copy.

## Acceptance criteria

#### AC1 — All UI strings are reviewed Arabic copy (AC-24)

Given the Arabic locale is active, when the UI is rendered, then all visible strings are reviewed, natural Arabic text — not placeholder, transliterated, or machine-translated copy.

#### AC2 — No missing translation keys (AC-24)

Given the Arabic locale is active, when the UI is rendered across all screens, then no translation key is displayed raw (e.g. `SECTION_NAME`).

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-24 | Unit | `ArabicTranslationKeysComplete` | Given the Arabic translation file, when compared against the English keys, then all keys are present | No missing keys |
| TC-02 | AC-24 | Unit | `NoRawTranslationKeysInRenderedHtml` | Given Arabic locale, when the app renders any page, then no raw key pattern (ALL_CAPS_UNDERSCORED) appears in the DOM | No raw keys |
| TC-03 | AC-24 | Manual | `ArabicCopyReviewed` | Given a native Arabic speaker reviews the UI, when all screens are inspected, then copy is natural and accurate | Reviewer sign-off |

## Notes

- TC-03 is a manual review; it cannot be automated.
- Translation files should be validated against the English source to ensure completeness.
- Follow the existing i18n patterns in the Angular app.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
