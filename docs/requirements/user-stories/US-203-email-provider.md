# US-203 · Configure email provider integration

| Field | Value |
|---|---|
| **Story** | `US-203` |
| **Epic** | [EPIC-03 Communication channels](../epics/EPIC-03-communication-channels.md) |
| **Feature** | *(no frontend feature — infrastructure only)* |
| **Layer** | Backend |
| **Ships with** | No frontend counterpart (infrastructure only) |
| **Actor** | System |
| **Priority** | P0 |
| **Sprint** | [9 — Email channel](../delivery-plan.md#sprint-9--email-channel) · Slice S5 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-11.5 |
| **Spec criteria** | AC-11.5 |
| **Depends on** | None |

## Story

**As a system**, **I want** email sent through a configured provider, **so that** outbound communication works reliably.

## Business rules

No BRD `BR-n` covers this directly. Derived from the cited criterion:

- Email must be sent via a configurable provider with retry and exponential backoff on transient failures (from AC-11.5).

## Acceptance criteria

#### AC1 — Send email via configured provider (spec AC-11.5)

Given an email provider is configured (SMTP/relay settings), when outbound email is triggered, then the email is sent via the configured provider.

#### AC2 — Retry with backoff

Given an email send fails with a transient error, when retry is attempted, then up to 3 retries occur with exponential backoff (1s, 2s, 4s).

#### AC3 — Non-transient failure

Given an email send fails with a non-transient error (e.g., invalid address), when no retry is possible, then the failure is logged and the caller is notified without retry.

## SQL tables

`EmailProviderConfigurations` — stores provider settings (may extend existing platform configuration):

```sql
CREATE TABLE [dbo].[EmailProviderConfigurations] (
    [Id]             BIGINT           IDENTITY(1,1) NOT NULL,
    [ProviderType]   NVARCHAR(50)     NOT NULL,
    [Host]           NVARCHAR(256)    NOT NULL,
    [Port]           INT              NOT NULL,
    [UseSsl]         BIT              NOT NULL DEFAULT 1,
    [Username]       NVARCHAR(256)    NULL,
    [PasswordSecret] NVARCHAR(512)    NULL,
    [FromAddress]    NVARCHAR(256)    NOT NULL,
    [FromName]       NVARCHAR(256)    NULL,
    [IsDefault]      BIT              NOT NULL DEFAULT 0,
    [IsActive]       BIT              NOT NULL DEFAULT 1,
    [CreatedAt]      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_EmailProviderConfigurations] PRIMARY KEY ([Id])
);
```

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-11.5 | Unit | `EmailProvider_Send_SendsViaConfiguredProvider` | Given a mock provider configured, when SendAsync is called, then the mock receives the email | Mock provider's SendAsync called with correct subject, body, recipients |
| TC-02 | AC-11.5 | Unit | `EmailProvider_TransientFailure_RetriesWithBackoff` | Given a provider that fails twice then succeeds, when email is sent, then retry occurs with backoff | 3 attempts made; timing consistent with exponential backoff |
| TC-03 | AC-11.5 | Unit | `EmailProvider_NonTransientFailure_ThrowsImmediately` | Given a provider returning an invalid-address error, when email is sent, then no retry and exception thrown | Single attempt only; exception propagated to caller |
| TC-04 | AC-11.5 | Unit | `EmailProvider_NoConfiguration_Throws` | Given no email provider is configured, when email send is attempted, then InvalidOperationException is thrown | Configuration error surfaced at startup or call time |

## Notes

- For the MVP the SMTP provider is sufficient; SendGrid or other providers can be added later.
- Password/secret storage should use the platform's existing secret management (PlatformSettings or Azure Key Vault reference).
- The `Mock` provider type enables integration testing without a real SMTP server.

## Open questions

None.

## Status evidence

Not yet implemented.

Status is set from what is committed and executed, never from what is planned.
