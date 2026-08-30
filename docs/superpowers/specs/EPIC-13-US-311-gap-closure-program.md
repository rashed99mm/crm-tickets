# Gap Closure Program

**Date:** 2026-08-27  
**Status:** Draft for approval  
**Purpose:** Define every named gap as an independently deliverable SDD story.

## Problem

The platform is substantially implemented, but the remaining stories are not consistently
implemented, tested, or evidenced. The gaps span SLA automation, email, reports, permissions,
branch isolation, responsive/localised UI, branding, and the terminal E2E journey.

This program does not replace the existing story files. It gives each named story its own approved
implementation boundary, plan folder, task folder, tests, and evidence record.

## Scope

| Group | Stories |
|---|---|
| SLA and automation | US-215, US-217, US-218, US-219, US-220 |
| Dashboard/platform UI | US-129, US-311, US-312, US-313, US-314 |
| Reports | US-605, US-606, US-607, US-608, US-609, US-610 |
| Permissions | US-804, US-805 |
| Platform/branches | US-122, US-306, US-310 |
| Communication | US-202, US-203, US-204, US-205 |

The 2026-08-29 frontend audit adds a concrete gap register for the scorecard findings:
[`EPIC-13-US-311-ui-gap-closure-sdd.md`](EPIC-13-US-311-ui-gap-closure-sdd.md). That addendum is now the
owner for visible static/decorative UI defects while this program remains the owner for larger
backend-backed story delivery.

The full 100% backend, frontend, Stitch, and evidence program is specified separately in
[`EPIC-12-US-000-fullstack-gap-closure-sdd.md`](EPIC-12-US-000-fullstack-gap-closure-sdd.md), with execution
tasks under `docs/superpowers/plans/EPIC-12-US-000-fullstack-gap-closure/`.

Previously cut SLA stories US-215, US-217, US-219 and US-220 are reopened by this specification.
US-306 remains blocked until OQ-5 is resolved. US-310 begins with a mini-spec because its current
story does not define sufficient branch-administration behavior.

## Assumptions

- **A1:** Existing Clean Architecture, `Response<T>`, `IRepository<T>`, `IUserContext`, MediatR,
  EF Core, Angular standalone components, signals, and shared translation tokens remain mandatory.
- **A2:** Every vertical story ships backend, frontend, tests, build evidence, and updated story
  status. API-only stories explicitly record why they have no screen.
- **A3:** All timestamps are UTC on the wire and all visible UI strings use `TranslatePipe`.
- **A4:** Existing platform entities are extended only through migrations; no destructive migration
  or silent data loss is allowed.
- **A5:** Email provider credentials, AI/API keys, and export data are secrets or sensitive data;
  they are never logged or returned in an error envelope.
- **A6:** Previously cut items are now product-approved for specification and implementation.
- **A7:** US-306 cannot be implemented until OQ-5 records branch ownership, default branch behavior,
  and the exact administrator bypass rule.

## Acceptance Criteria

The original story files remain authoritative. This program adds delivery criteria that apply to
every story:

- **AC-G1:** Every story has one folder under `docs/superpowers/plans/` named after its story.
- **AC-G2:** Every implementation task names the original story and exact AC it satisfies.
- **AC-G3:** Every AC has a failing test before implementation and a recorded passing command after.
- **AC-G4:** Every endpoint has authorization, validation, envelope, trace ID, and negative-path
  coverage appropriate to its layer.
- **AC-G5:** Every UI story has component tests, RTL checks, translation checks, and a clean build.
- **AC-G6:** No story is marked done until its `Status evidence` section contains real command output.
- **AC-G7:** The delivery plan and rubric traceability are updated only after execution evidence exists.

## Dependency Order

1. US-122 contract hardening and US-202 message timeline evidence.
2. US-203 email provider configuration.
3. US-204 inbound email ingestion.
4. US-205 outbound email.
5. US-215 business-hours calendar.
6. US-217 warning and US-218 complete escalation.
7. US-219 notifications and US-220 auto-assignment.
8. US-608 report scoping and US-610 filters.
9. US-605 CSAT, US-606 management dashboard, US-607 live queue.
10. US-609 export.
11. US-804 permission entity, then US-805 permission UI.
12. Resolve OQ-5, then US-306; specify and implement US-310.
13. US-311 responsive, US-312 RTL, US-313 reviewed Arabic, US-314 branding.
14. US-129 terminal browser journey after the dependent workflow is stable.

## Universal Done Gate

1. Backend criteria have tests naming the criteria.
2. Frontend criteria have tests naming the criteria.
3. Targeted tests and the affected full suite were run; output is pasted.
4. Build is clean under warnings-as-errors.
5. Security and edge-case checklist is completed.
6. Story status and evidence are updated from observed results.
7. One logical conventional commit contains only that story.

## Out of Scope

New backend capabilities for WhatsApp/SMS/email inboxes, ERP connectors, agent tasks, quick replies,
internal chat, profile preferences, team administration, and global language defaults remain out of
scope for this program until their dedicated SDD stories are approved. Existing live-chat frontend
defects are covered by the 2026-08-29 UI gap addendum.
