# US-401 · Customer Registration

| Field | Value |
|---|---|
| **Story** | `US-401` |
| **Epic** | [EPIC-07 Customer Portal](../epics/EPIC-07.md) |
| **Feature** | [`FEAT-15` Customer Portal](../delivery-plan.md#feat-15--customer-portal) |
| **Layer** | Backend |
| **Ships with** | [US-402](./US-402-customer-login.md) *(backend)*, [US-403](./US-403-customer-authorization.md) *(backend)* |
| **Actor** | Customer |
| **Priority** | P0 |
| **Sprint** | [10 — Customer portal](../delivery-plan.md#sprint-10-customer-portal) · Slice S3 |
| **Estimate** | 5 points |
| **Status** | `not started` |
| **BRD requirements** | FR-8.1, BR-20 |
| **Spec criteria** | AC-1 |
| **Depends on** | — |

## Story

**As a customer**, **I want** to register for the portal, **so that** I can submit and track requests.

## Business rules

- BR-20 — Customer scoped to own records (BRD).

## Acceptance criteria

#### AC1 — Registration creates credentials and returns JWT (spec AC-1)

Given customer registers with valid data, when registration submitted, then a Customer record and portal credentials are created, and a JWT is returned.

## SQL tables

`Customers` — customer account:

```sql
CREATE TABLE [dbo].[Customers] (
    [Id]            UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [Email]         NVARCHAR(256)    NOT NULL,
    [DisplayName]   NVARCHAR(256)    NOT NULL,
    [PhoneNumber]   NVARCHAR(50)     NULL,
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_Customers_Email] UNIQUE ([Email])
);
```

`AspNetUsers` — portal credentials (Identity):

```sql
CREATE TABLE [dbo].[AspNetUsers] (
    [Id]            NVARCHAR(450)  NOT NULL,
    [Email]         NVARCHAR(256)  NOT NULL,
    [UserName]      NVARCHAR(256)  NOT NULL,
    [PasswordHash]  NVARCHAR(MAX)  NOT NULL,
    [CustomerId]    UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUsers_Customers] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-1 | Integration | `Registration_ReturnsJwt` | Given valid registration payload, when POST /api/portal/register, then 201 with JWT | Response contains token and expiresAt |
| TC-02 | AC-1 | Integration | `Registration_CreatesCustomerRecord` | Given valid registration payload, when registered, then Customers table has row | Row has correct email, display name |
| TC-03 | AC-1 | Integration | `Registration_CreatesIdentityUser` | Given valid registration, when registered, then AspNetUsers has row linked to Customer | PasswordHash non-null, CustomerId FK valid |
| TC-04 | AC-1 | Integration | `Registration_DuplicateEmail_ReturnsConflict` | Given existing email, when registering same email, then 409 Conflict | Error message indicates duplicate |

## Notes

Registration flow: create Customer row, create Identity user in portal role, generate JWT with customerId claim.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
