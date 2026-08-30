# US-402 · Customer Login Endpoint

| Field | Value |
|---|---|
| **Story** | `US-402` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Backend |
| **Ships with** | [US-403](./US-403-customer-authorization.md) *(backend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.1, BR-20 |
| **Spec criteria** | AC-2 |
| **Depends on** | [US-401](./US-401-customer-registration.md) |

## Story

**As a customer**, **I want** to log in, **so that** I can access my data securely.

## Business rules

- BR-20 — Customer scoped to own records (BRD).

## Acceptance criteria

#### AC1 — Login issues JWT with customerId claim (spec AC-2)

Given valid customer credentials, when login submitted, then a JWT is issued containing the customerId claim.

## SQL tables

`AspNetUsers` — queried for credential validation:

```sql
CREATE TABLE [dbo].[AspNetUsers] (
    [Id]            NVARCHAR(450)   NOT NULL,
    [Email]         NVARCHAR(256)   NOT NULL,
    [PasswordHash]  NVARCHAR(MAX)   NOT NULL,
    [CustomerId]    UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUsers_Customers] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-2 | Integration | `Login_ValidCredentials_ReturnsJwt` | Given registered customer with valid password, when POST /api/portal/login, then 200 with JWT | Token contains customerId claim |
| TC-02 | AC-2 | Integration | `Login_InvalidCredentials_Returns401` | Given wrong password, when login submitted, then 401 Unauthorized | Error message generic (no email enumeration) |
| TC-03 | AC-2 | Integration | `Login_NonexistentEmail_Returns401` | Given unregistered email, when login submitted, then 401 Unauthorized | Same response as invalid password |

## Notes

Login uses Identity password verification. JWT is issued with customerId claim for downstream scoping.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
