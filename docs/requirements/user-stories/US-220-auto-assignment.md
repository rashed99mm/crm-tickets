# US-220 · Auto-Assign Tickets by Rule

| Field | Value |
|---|---|
| **Story** | `US-220` |
| **Epic** | [EPIC-05 SLA & Escalation](../epics/EPIC-05-sla-and-automation.md) |
| **Feature** | [`FEAT-14` SLA & Escalation](../delivery-plan.md#feat-14--sla-escalation) |
| **Layer** | Backend |
| **Ships with** | [US-221](./US-221-supervisor-override.md) *(backend)* |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [8 — SLA and automation](../delivery-plan.md#sprint-8-sla-and-automation) · Slice S2 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-5.6 |
| **Spec criteria** | AC-5.6 |
| **Depends on** | [US-201](./US-201-notification-service.md) |

## Story

**As a system**, **I want** to auto-assign new tickets using configurable rules, **so that** queue wait is minimised and tickets reach the most appropriate agent quickly.

## Business rules

- BR-24 — Auto-assignment supports round-robin or load-based strategies (BRD).
- BR-25 — Tickets are only auto-assigned to agents with the appropriate role and within the matching branch/category (BRD).

## Acceptance criteria

#### AC1 — Auto-Assign New Ticket (spec AC-5.6)

Given a new ticket is created, when auto-assignment is enabled, then the ticket is assigned to the next available agent using the configured strategy.

#### AC2 — Round-Robin Assignment (spec AC-5.6)

Given round-robin strategy is configured, when a ticket is created, then the ticket is assigned to the agent next in rotation within the matching pool.

#### AC3 — Load-Based Assignment (spec AC-5.6)

Given load-based strategy is configured, when a ticket is created, then the ticket is assigned to the agent with the fewest active tickets in the matching pool.

## SQL tables

`AssignmentStrategies` — configuration for auto-assignment:

```sql
CREATE TABLE [dbo].[AssignmentStrategies] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [StrategyType]    NVARCHAR(50)     NOT NULL,
    [BranchId]        UNIQUEIDENTIFIER NULL,
    [CategoryId]      UNIQUEIDENTIFIER NULL,
    [IsActive]        BIT              NOT NULL DEFAULT 1,
    [CreatedAt]       DATETIME2        NOT NULL,
    [UpdatedAt]       DATETIME2        NOT NULL,
    CONSTRAINT [PK_AssignmentStrategies] PRIMARY KEY ([Id])
);
```

`AgentRotations` — round-robin rotation tracking:

```sql
CREATE TABLE [dbo].[AgentRotations] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [StrategyId]      UNIQUEIDENTIFIER NOT NULL,
    [AgentId]         UNIQUEIDENTIFIER NOT NULL,
    [LastAssignedAt]  DATETIME2        NULL,
    [SortOrder]       INT              NOT NULL,
    CONSTRAINT [PK_AgentRotations] PRIMARY KEY ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-5.6 | Unit | `AutoAssign_RoundRobin_ShouldAssignNextAgent` | Given 3 agents in rotation with the next in sequence being Agent B, when a ticket is created, then the ticket is assigned to Agent B | Ticket assigned to Agent B |
| TC-02 | AC-5.6 | Unit | `AutoAssign_LoadBased_ShouldAssignLeastBusy` | Given agents with 5, 3, and 7 active tickets, when a ticket is created, then the ticket is assigned to the agent with 3 active tickets | Ticket assigned to least loaded agent |
| TC-03 | AC-5.6 | Unit | `AutoAssign_ShouldRespectBranchScope` | Given agents in different branches, when a ticket is created for branch A, then only branch A agents are considered for assignment | Correct scope enforced |
| TC-04 | AC-5.6 | Unit | `AutoAssign_AllAgentsAtCapacity_ShouldNotAssign` | Given all agents at maximum capacity, when a ticket is created, then the ticket remains unassigned and is queued | Ticket unassigned, queued |

## Notes

Assignment runs as part of ticket creation via a MediatR pipeline behaviour or notification handler. Strategy configuration is managed via PlatformSettings or a dedicated admin screen.

## Open questions

None.

## Status evidence

**Not built** — a genuinely separate capability (round-robin/load-based assignment strategy, new
config tables) unrelated to the breach/escalation loop `FEAT-17` closed. See
`docs/superpowers/plans/EPIC-05-US-218-feat-17-sla-escalation/README.md`.

Status is set from what is committed and executed, never from what is planned.
