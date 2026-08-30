# Slice S1 — criteria coverage

Every acceptance criterion in the two specs governing slice S1, and the single story that claims it.

**This table is the check that matters.** An uncovered criterion is invisible work; a criterion
claimed twice means two stories will each assume the other did it. Both are verified by script
rather than by eye — see [the folder conventions](./README.md#verifying-this-folder).

| | |
|---|---|
| Criteria | **101** — 68 `AC-n` from the ticket-lifecycle spec, 33 `FND-n` from the backend-foundation spec |
| Stories | 49 across 5 sprints ([delivery plan](./delivery-plan.md)) |
| Estimate | 216 points |
| Story status | 32 not started · 16 superseded · 1 done |

## Ticket-lifecycle criteria (`AC-1`–`AC-68`)

| Criterion | Story | Epic | Sprint | Status |
|---|---|---|---|---|
| AC-1 | [US-112](./user-stories/US-112-staff-sign-in.md) | EPIC-09 | 1 | `done` |
| AC-2 | [US-113](./user-stories/US-113-failed-sign-in-reveals-nothing.md) | EPIC-09 | 1 | `superseded` |
| AC-3 | [US-112](./user-stories/US-112-staff-sign-in.md) | EPIC-09 | 1 | `done` |
| AC-4 | [US-114](./user-stories/US-114-role-permissions-refuse.md) | EPIC-09 | 1 | `superseded` |
| AC-5 | [US-115](./user-stories/US-115-credentials-never-emitted.md) | EPIC-09 | 1 | `superseded` |
| AC-6 | [US-113](./user-stories/US-113-failed-sign-in-reveals-nothing.md) | EPIC-09 | 1 | `superseded` |
| AC-7 | [US-001](./user-stories/US-001-create-a-customer.md) | EPIC-01 | 2 | `not started` |
| AC-8 | [US-001](./user-stories/US-001-create-a-customer.md) | EPIC-01 | 2 | `not started` |
| AC-9 | [US-116](./user-stories/US-116-duplicate-email-is-a-conflict.md) | EPIC-01 | 2 | `not started` |
| AC-10 | [US-004](./user-stories/US-004-find-a-customer.md) | EPIC-01 | 2 | `not started` |
| AC-11 | [US-004](./user-stories/US-004-find-a-customer.md) | EPIC-01 | 2 | `not started` |
| AC-12 | [US-002](./user-stories/US-002-read-and-correct-a-customer.md) | EPIC-01 | 2 | `not started` |
| AC-13 | [US-004](./user-stories/US-004-find-a-customer.md) | EPIC-01 | 2 | `not started` |
| AC-14 | [US-002](./user-stories/US-002-read-and-correct-a-customer.md) | EPIC-01 | 2 | `not started` |
| AC-15 | [US-117](./user-stories/US-117-delete-guard.md) | EPIC-01 | 2 | `not started` |
| AC-16 | [US-117](./user-stories/US-117-delete-guard.md) | EPIC-01 | 2 | `not started` |
| AC-17 | [US-007](./user-stories/US-007-record-a-note.md) | EPIC-01 | 5 | `not started` |
| AC-18 | [US-007](./user-stories/US-007-record-a-note.md) | EPIC-01 | 5 | `not started` |
| AC-19 | [US-007](./user-stories/US-007-record-a-note.md) | EPIC-01 | 5 | `not started` |
| AC-20 | [US-007](./user-stories/US-007-record-a-note.md) | EPIC-01 | 5 | `not started` |
| AC-21 | [US-006](./user-stories/US-006-read-notes-newest-first.md) | EPIC-01 | 5 | `not started` |
| AC-22 | [US-008](./user-stories/US-008-attach-a-file.md) | EPIC-01 | 5 | `not started` |
| AC-23 | [US-008](./user-stories/US-008-attach-a-file.md) | EPIC-01 | 5 | `not started` |
| AC-24 | [US-008](./user-stories/US-008-attach-a-file.md) | EPIC-01 | 5 | `not started` |
| AC-25 | [US-131](./user-stories/US-131-hostile-filename-cannot-escape.md) | EPIC-01 | 5 | `not started` |
| AC-26 | [US-132](./user-stories/US-132-retrieve-and-remove-attachment.md) | EPIC-01 | 5 | `not started` |
| AC-27 | [US-008](./user-stories/US-008-attach-a-file.md) | EPIC-01 | 5 | `not started` |
| AC-28 | [US-132](./user-stories/US-132-retrieve-and-remove-attachment.md) | EPIC-01 | 5 | `not started` |
| AC-29 | [US-009](./user-stories/US-009-raise-a-ticket.md) | EPIC-02 | 2 | `not started` |
| AC-30 | [US-009](./user-stories/US-009-raise-a-ticket.md) | EPIC-02 | 2 | `not started` |
| AC-31 | [US-009](./user-stories/US-009-raise-a-ticket.md) | EPIC-02 | 2 | `not started` |
| AC-32 | [US-013](./user-stories/US-013-filter-the-queue.md) | EPIC-02 | 2 | `not started` |
| AC-33 | [US-013](./user-stories/US-013-filter-the-queue.md) | EPIC-02 | 2 | `not started` |
| AC-34 | [US-035](./user-stories/US-035-agent-sees-own-work.md) | EPIC-04 | 2 | `not started` |
| AC-35 | [US-010](./user-stories/US-010-ticket-detail.md) | EPIC-02 | 3 | `not started` |
| AC-36 | [US-010](./user-stories/US-010-ticket-detail.md) | EPIC-02 | 3 | `not started` |
| AC-37 | [US-016](./user-stories/US-016-move-along-the-lifecycle.md) | EPIC-02 | 3 | `not started` |
| AC-38 | [US-118](./user-stories/US-118-refuse-undefined-transitions.md) | EPIC-02 | 3 | `not started` |
| AC-39 | [US-118](./user-stories/US-118-refuse-undefined-transitions.md) | EPIC-02 | 3 | `not started` |
| AC-40 | [US-026](./user-stories/US-026-reopen-and-refuse-lost-updates.md) | EPIC-02 | 3 | `not started` |
| AC-41 | [US-026](./user-stories/US-026-reopen-and-refuse-lost-updates.md) | EPIC-02 | 3 | `not started` |
| AC-42 | [US-014](./user-stories/US-014-supervisor-assigns-work.md) | EPIC-02 | 3 | `not started` |
| AC-43 | [US-119](./user-stories/US-119-agent-cannot-assign.md) | EPIC-02 | 3 | `not started` |
| AC-44 | [US-014](./user-stories/US-014-supervisor-assigns-work.md) | EPIC-02 | 3 | `not started` |
| AC-45 | [US-120](./user-stories/US-120-status-change-belongs-to-assignee.md) | EPIC-02 | 3 | `not started` |
| AC-46 | [US-120](./user-stories/US-120-status-change-belongs-to-assignee.md) | EPIC-02 | 3 | `not started` |
| AC-47 | [US-120](./user-stories/US-120-status-change-belongs-to-assignee.md) | EPIC-02 | 3 | `not started` |
| AC-48 | [US-121](./user-stories/US-121-every-change-recorded-immutably.md) | EPIC-02 | 3 | `not started` |
| AC-49 | [US-121](./user-stories/US-121-every-change-recorded-immutably.md) | EPIC-02 | 3 | `not started` |
| AC-50 | [US-022](./user-stories/US-022-read-ticket-history.md) | EPIC-02 | 3 | `not started` |
| AC-51 | [US-122](./user-stories/US-122-stable-code-per-condition.md) | EPIC-12 | 4 | `not started` |
| AC-52 | [US-123](./user-stories/US-123-diagnosable-without-leaking.md) | EPIC-12 | 4 | `not started` |
| AC-53 | [US-123](./user-stories/US-123-diagnosable-without-leaking.md) | EPIC-12 | 4 | `not started` |
| AC-54 | [US-124](./user-stories/US-124-unambiguous-wire-format.md) | EPIC-12 | 4 | `superseded` |
| AC-55 | [US-125](./user-stories/US-125-sign-in-and-land-on-work.md) | EPIC-04 | 1 | `superseded` |
| AC-56 | [US-125](./user-stories/US-125-sign-in-and-land-on-work.md) | EPIC-04 | 1 | `superseded` |
| AC-57 | [US-038](./user-stories/US-038-usable-ticket-list.md) | EPIC-04 | 2 | `not started` |
| AC-58 | [US-126](./user-stories/US-126-empty-never-looks-like-failure.md) | EPIC-04 | 2 | `not started` |
| AC-59 | [US-127](./user-stories/US-127-validated-create-ticket-form.md) | EPIC-04 | 2 | `not started` |
| AC-60 | [US-127](./user-stories/US-127-validated-create-ticket-form.md) | EPIC-04 | 2 | `not started` |
| AC-61 | [US-128](./user-stories/US-128-ticket-detail-with-guarded-actions.md) | EPIC-04 | 3 | `not started` |
| AC-62 | [US-130](./user-stories/US-130-notes-in-customer-detail.md) | EPIC-01 | 5 | `not started` |
| AC-63 | [US-093](./user-stories/US-093-bilingual-instant-switching.md) | EPIC-12 | 4 | `not started` |
| AC-64 | [US-129](./user-stories/US-129-end-to-end-journey.md) | EPIC-04 | 4 | `not started` |
| AC-65 | [US-133](./user-stories/US-133-attachments-in-customer-detail.md) | EPIC-01 | 5 | `not started` |
| AC-66 | [US-122](./user-stories/US-122-stable-code-per-condition.md) | EPIC-12 | 4 | `not started` |
| AC-67 | [US-113](./user-stories/US-113-failed-sign-in-reveals-nothing.md) | EPIC-09 | 1 | `superseded` |
| AC-68 | [US-093](./user-stories/US-093-bilingual-instant-switching.md) | EPIC-12 | 4 | `not started` |

## Backend-foundation criteria (`FND-1`–`FND-32`, plus `FND-13a`)

`FND-13a` is a sub-lettered criterion appended by the spec after `FND-13`; it is counted and covered
like any other.

| Criterion | Story | Epic | Sprint | Status |
|---|---|---|---|---|
| FND-1 | [US-101](./user-stories/US-101-uniform-response-envelope.md) | EPIC-12 | 1 | `superseded` |
| FND-2 | [US-101](./user-stories/US-101-uniform-response-envelope.md) | EPIC-12 | 1 | `superseded` |
| FND-3 | [US-101](./user-stories/US-101-uniform-response-envelope.md) | EPIC-12 | 1 | `superseded` |
| FND-4 | [US-102](./user-stories/US-102-outcome-to-status-mapping.md) | EPIC-12 | 1 | `superseded` |
| FND-5 | [US-101](./user-stories/US-101-uniform-response-envelope.md) | EPIC-12 | 1 | `superseded` |
| FND-6 | [US-103](./user-stories/US-103-trace-id-and-timestamp.md) | EPIC-12 | 1 | `superseded` |
| FND-7 | [US-103](./user-stories/US-103-trace-id-and-timestamp.md) | EPIC-12 | 1 | `superseded` |
| FND-8 | [US-102](./user-stories/US-102-outcome-to-status-mapping.md) | EPIC-12 | 1 | `superseded` |
| FND-9 | [US-104](./user-stories/US-104-field-keyed-validation-errors.md) | EPIC-12 | 1 | `superseded` |
| FND-10 | [US-104](./user-stories/US-104-field-keyed-validation-errors.md) | EPIC-12 | 1 | `superseded` |
| FND-11 | [US-104](./user-stories/US-104-field-keyed-validation-errors.md) | EPIC-12 | 1 | `superseded` |
| FND-12 | [US-105](./user-stories/US-105-reflection-free-validation-pipeline.md) | EPIC-12 | 1 | `superseded` |
| FND-13 | [US-105](./user-stories/US-105-reflection-free-validation-pipeline.md) | EPIC-12 | 1 | `superseded` |
| FND-13a | [US-105](./user-stories/US-105-reflection-free-validation-pipeline.md) | EPIC-12 | 1 | `superseded` |
| FND-14 | [US-106](./user-stories/US-106-bilingual-message-catalogue.md) | EPIC-12 | 1 | `superseded` |
| FND-15 | [US-106](./user-stories/US-106-bilingual-message-catalogue.md) | EPIC-12 | 1 | `superseded` |
| FND-16 | [US-106](./user-stories/US-106-bilingual-message-catalogue.md) | EPIC-12 | 1 | `superseded` |
| FND-17 | [US-106](./user-stories/US-106-bilingual-message-catalogue.md) | EPIC-12 | 1 | `superseded` |
| FND-18 | [US-107](./user-stories/US-107-every-code-has-a-message.md) | EPIC-12 | 1 | `superseded` |
| FND-19 | [US-107](./user-stories/US-107-every-code-has-a-message.md) | EPIC-12 | 1 | `superseded` |
| FND-20 | [US-106](./user-stories/US-106-bilingual-message-catalogue.md) | EPIC-12 | 1 | `superseded` |
| FND-21 | [US-107](./user-stories/US-107-every-code-has-a-message.md) | EPIC-12 | 1 | `superseded` |
| FND-22 | [US-108](./user-stories/US-108-domain-base-types.md) | EPIC-12 | 1 | `superseded` |
| FND-23 | [US-109](./user-stories/US-109-auditing-and-soft-delete.md) | EPIC-12 | 1 | `superseded` |
| FND-24 | [US-109](./user-stories/US-109-auditing-and-soft-delete.md) | EPIC-12 | 1 | `superseded` |
| FND-25 | [US-109](./user-stories/US-109-auditing-and-soft-delete.md) | EPIC-12 | 1 | `superseded` |
| FND-26 | [US-109](./user-stories/US-109-auditing-and-soft-delete.md) | EPIC-12 | 1 | `superseded` |
| FND-27 | [US-108](./user-stories/US-108-domain-base-types.md) | EPIC-12 | 1 | `superseded` |
| FND-28 | [US-108](./user-stories/US-108-domain-base-types.md) | EPIC-12 | 1 | `superseded` |
| FND-29 | [US-110](./user-stories/US-110-dependency-rule-enforced.md) | EPIC-12 | 1 | `superseded` |
| FND-30 | [US-111](./user-stories/US-111-api-documentation-and-health.md) | EPIC-12 | 1 | `superseded` |
| FND-31 | [US-111](./user-stories/US-111-api-documentation-and-health.md) | EPIC-12 | 1 | `superseded` |
| FND-32 | [US-111](./user-stories/US-111-api-documentation-and-health.md) | EPIC-12 | 1 | `superseded` |

## What the status column says about the slice

**Rewritten 2026-08-25 after the platform pivot.** The backend these criteria were verified against
was replaced ([ADR-0009](../adr/0009-adopt-the-support-platform-as-the-crm-baseline.md)), so almost every
status that read `done` or `partial` now reads `superseded`.

`superseded` does **not** mean the requirement went away. It means *this codebase has not been shown
to meet it*. Sixteen stories are in that state because their implementation is archived rather than
running. One — `US-112`, staff sign-in — is `done` against the new baseline, verified by live request.

Reading this table honestly: the criteria in the two S1 specs describe a support CRM the current
backend does not yet implement. What the backend does implement is a knowledge base, staff accounts,
notifications, settings and integration configuration, none of which those specs enumerate. Closing
that gap is the ticket workflow (`BASE-11`–`BASE-14`) plus re-verification of the criteria the
platform happens to satisfy already.

The count that matters is not how many stories are `not started`; it is that **65 of 68 `AC-n` and 32
of 33 `FND-n` are currently unproven against the shipped code.**
