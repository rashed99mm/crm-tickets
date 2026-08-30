# US-311 — Responsive Layout

## Problem
All primary screens must remain usable on mobile, tablet, and desktop widths.

## Assumptions
- A1: Supported widths are 375px, 768px, and desktop.

## Out of scope
Native mobile applications.

## Acceptance Criteria
- AC-311.1: Primary screens have no page-level horizontal overflow at supported widths.
- AC-311.2: Sidebar collapses accessibly on small screens.
- AC-311.3: Tables/forms preserve readable content and focus order.

## Design
Use existing Tailwind breakpoints, logical properties, a keyboard-accessible mobile menu, and viewport tests. Original story: `EPIC-13-US-311-responsive-layout.md` / AC-22.
