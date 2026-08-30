# US-210 · SLA Policy Entity

| Field | Value |
|---|---|
| **Story** | `US-210` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-211](./US-211-sla-event-entity.md) *(Backend)* |
| **Actor** | Admin |
| **Priority** | P0 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 3 points |
| **Status** | `done` |
| **BRD requirements** | FR-5.1, BR-01 |
| **Spec criteria** | AC-5.1 |
| **Depends on** | [US-201](./US-201-ticket-entity.md) |

## Story

**As an admin**, **I want** to define SLA targets per priority, **so that** commitments are enforced.

## Business rules

- BR-01 — SLA policies define response and resolution targets per priority level (BRD).

## Acceptance criteria

#### AC1 — Create SLA Policy (spec AC-5.1)

Given an SLA policy payload with Priority, ResponseTargetHours, and ResolutionTargetHours, when the policy is created, then the policy is stored with Priority, ResponseTargetHours, ResolutionTargetHours, and optional CategoryId and optional BranchId.

## SQL tables

`SLAPolicies` — stores SLA target definitions per priority:

```sql
CREATE TABLE [dbo].[SLAPolicies] (
    [Id]                    UNIQUEIDENTIFIER NOT NULL,
    [Priority]              NVARCHAR(50)     NOT NULL,
    [ResponseTargetHours]   DECIMAL(10,2)    NOT NULL,
    [ResolutionTargetHours] DECIMAL(10,2)    NOT NULL,
    [CategoryId]            UNIQUEIDENTIFIER NULL,
    [BranchId]              UNIQUEIDENTIFIER NULL,
    [IsActive]              BIT              NOT NULL DEFAULT 1,
    [CreatedAt]             DATETIME2        NOT NULL,
    [UpdatedAt]             DATETIME2        NOT NULL,
    CONSTRAINT [PK_SLAPolicies] PRIMARY KEY ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.1 | Unit | `CreatePolicy_ShouldStorePolicy` | Given valid policy payload with Priority, ResponseTargetHours, ResolutionTargetHours, when policy is created, then policy is stored with all fields | Policy persisted with correct Priority, ResponseTargetHours, ResolutionTargetHours |

## Notes

This entity is the foundation for SLA target computation (US-212) and breach detection (US-216). The combination of Priority + CategoryId + BranchId should be unique per active policy.

## Open questions

None.

## Status evidence

Shipped `FEAT-17` first slice — `SLAPolicy` entity, migration reviewed. See
`docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-tracking/README.md`.

Status is set from what is committed and executed, never from what is planned.
