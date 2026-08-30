# US-306 — Branch-Scoped Queries

## Problem
Ticket and customer queries do not yet enforce branch data isolation.

## Assumptions
- A1: OQ-5 first defines branch ownership, users without branches, multi-branch users, and admin bypass.

## Out of scope
Any implementation before OQ-5 approval.

## Acceptance Criteria
- AC-306.1: Same-branch users see permitted ticket/customer rows.
- AC-306.2: Cross-branch rows are excluded or refused according to OQ-5.
- AC-306.3: The approved administrator bypass is explicit and audited.

## Design
After an ADR closes OQ-5, apply one branch-scope policy to repository predicates, ticket queries, customer queries, reports, and exports. Original story: `EPIC-13-US-306-branch-scoped-queries.md` / AC-17.
