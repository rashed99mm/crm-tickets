# US-215 — Business-Hours Calendar

## Problem
SLA due times currently cannot account for branch working hours, weekends, holidays, or timezone.

## Assumptions
- A1: A branch has one timezone and weekly calendar plus holiday exceptions.
- A2: Stored and transmitted timestamps remain UTC.

## Out of scope
Warning notifications and escalation policy progression.

## Acceptance Criteria
- AC-215.1: Given a calendar, when business duration is added, then non-working time is excluded.
- AC-215.2: Given invalid/overlapping intervals, then validation returns field errors.

## Design
Create `IBusinessHoursCalculator`, calendar persistence/API/UI, and replace raw SLA arithmetic with the calculator. Original story: `EPIC-05-US-215-business-hours-calendar.md` / AC-5.4.
