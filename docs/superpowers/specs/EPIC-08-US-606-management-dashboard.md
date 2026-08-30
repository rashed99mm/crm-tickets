# US-606 — Management Dashboard

## Problem
Managers lack one screen for authoritative support-performance summaries.

## Assumptions
- A1: Dashboard totals come from server queries, not client-side aggregation.
- A2: Date range and scope metadata are returned with the DTO.

## Out of scope
Live queue refresh, which is US-607.

## Acceptance Criteria
- AC-606.1: Given the selected scope and period, then summary cards show authoritative values.
- AC-606.2: Given an unauthorized scope, then no data is returned.

## Design
Add one dashboard query/DTO and compose the existing Command Center cards. Original story: `EPIC-08-US-606-management-dashboard.md` / AC-606, DSH-1.
