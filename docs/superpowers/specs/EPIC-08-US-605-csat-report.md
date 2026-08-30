# US-605 — CSAT Report

## Problem
Management cannot measure satisfaction by language and communication channel.

## Assumptions
- A1: Unanswered surveys are excluded from averages.
- A2: Report scope follows US-608.

## Out of scope
Predictive satisfaction scoring and AI-generated commentary.

## Acceptance Criteria
- AC-605.1: Given responses, then the report aggregates CSAT by language.
- AC-605.2: Given responses, then the report aggregates CSAT by channel.

## Design
Persist immutable survey responses, expose an authorized aggregate query, and render cards/tables with empty/error states. Original story: `EPIC-08-US-605-csat-report.md` / AC-605.
