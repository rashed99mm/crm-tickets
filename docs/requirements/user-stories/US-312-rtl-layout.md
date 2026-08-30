# US-312 · Full RTL Layout Correctness

| Field | Value |
|---|---|
| **Story** | `US-312` |
| **Epic** | [EPIC-04 Agent dashboard](../epics/EPIC-04.md) |
| **Feature** | [`FEAT-17` Localisation & branding](../delivery-plan.md#feat-17--localisation-branding) |
| **Layer** | Frontend |
| **Ships with** | — |
| **Actor** | User |
| **Priority** | P0 |
| **Sprint** | [14 — Localisation and branding](../delivery-plan.md#sprint-14-localisation-and-branding) · Slice S14 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-12.4 |
| **Spec criteria** | AC-23 |
| **Depends on** | — |

## Story

**As an Arabic user**, **I want** correct RTL layout, **so that** the interface feels native.

## Business rules

- No BRD BR-n covers this directly. Full RTL layout correctness for Arabic locale.

## Acceptance criteria

#### AC1 — RTL layout mirrors correctly (AC-23)

Given the Arabic locale is active, when the application renders, then the layout is fully mirrored: sidebar on the right, text alignment right-to-left, and all directional CSS properties (margin, padding, border) are correct.

#### AC2 — Icons and directional cues are mirrored (AC-23)

Given the Arabic locale is active, when the application renders, then directional icons (arrows, chevrons) are mirrored to match RTL reading direction.

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-23 | E2E | `RtlSidebarOnRight` | Given Arabic locale, when the app loads, then the sidebar is positioned on the right side | Sidebar on right |
| TC-02 | AC-23 | E2E | `RextAlignmentIsRtl` | Given Arabic locale, when text is rendered, then text-align and direction properties are RTL | direction: rtl, text-align: right |
| TC-03 | AC-23 | E2E | `RtlDirectionalIconsMirrored` | Given Arabic locale, when directional icons render, then they are horizontally flipped | Icons mirrored |
| TC-04 | AC-23 | Unit | `DirAttributeSetOnHtml` | Given Arabic locale, when the document renders, then `<html dir="rtl" lang="ar">` is present | dir and lang attributes set |

## Notes

- Use CSS logical properties (`margin-inline-start`, `padding-inline-end`) instead of physical properties (`margin-left`, `padding-right`) for automatic RTL support.
- Check the `dir` attribute is set on `<html>` element based on locale.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
