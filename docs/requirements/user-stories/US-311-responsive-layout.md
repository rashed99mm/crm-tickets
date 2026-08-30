# US-311 · Responsive Layout

| Field | Value |
|---|---|
| **Story** | `US-311` |
| **Epic** | [EPIC-04 Agent dashboard](../epics/EPIC-04.md) |
| **Feature** | [`FEAT-17` Localisation & branding](../delivery-plan.md#feat-17--localisation-branding) |
| **Layer** | Frontend |
| **Ships with** | — |
| **Actor** | User |
| **Priority** | P0 |
| **Sprint** | [14 — Localisation and branding](../delivery-plan.md#sprint-14-localisation-and-branding) · Slice S14 |
| **Estimate** | 8 points |
| **Status** | `not started` |
| **BRD requirements** | FR-12.6 |
| **Spec criteria** | AC-22 |
| **Depends on** | — |

## Story

**As a user**, **I want** the app to be usable on phone, tablet, and desktop, **so that** I can work from any device.

## Business rules

- No BRD BR-n covers this directly. Responsive design across breakpoints.

## Acceptance criteria

#### AC1 — Layout usable at all screen sizes (AC-22)

Given screen widths between 360px and 1440px, when the application is rendered, then the layout is usable at all sizes with appropriate spacing, readable text, and functional navigation.

#### AC2 — Sidebar collapse on small screens (AC-22)

Given the screen width is below the tablet breakpoint, when the application renders, then the sidebar collapses into a hamburger menu or overlay.

#### AC3 — Breakpoints defined (AC-22)

Given the application uses responsive design, when inspecting the CSS, then breakpoints are defined for mobile (<768px), tablet (768px–1024px), and desktop (>1024px).

## SQL tables

None — frontend story.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-22 | E2E | `LayoutRendersAtMobileWidth` | Given viewport 360px wide, when the app loads, then the sidebar is collapsed and main content is visible | Sidebar collapsed, content readable |
| TC-02 | AC-22 | E2E | `LayoutRendersAtTabletWidth` | Given viewport 768px wide, when the app loads, then the layout is usable with appropriate tablet spacing | Layout functional at 768px |
| TC-03 | AC-22 | E2E | `LayoutRendersAtDesktopWidth` | Given viewport 1440px wide, when the app loads, then the full sidebar and content area are visible | Full layout visible |
| TC-04 | AC-22 | E2E | `SidebarTogglesOnMobile` | Given viewport 360px wide, when the hamburger icon is clicked, then the sidebar opens as an overlay | Sidebar overlay visible |
| TC-05 | AC-22 | Unit | `BreakpointsAreDefined` | Given the responsive stylesheet, when inspected, then media queries exist for the three breakpoints | Media queries present |

## Notes

- Follow the existing Angular layout conventions.
- Check the mockups in `stitch_smart_support_ticketing_crm` for reference layouts.
- Use CSS container queries or media queries, not JavaScript-based resizing.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
