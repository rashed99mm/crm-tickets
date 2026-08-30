# US-203 — Email Provider

## Problem
The platform cannot send through a configured email provider with safe retry behavior.

## Assumptions
- A1: Provider configuration uses the existing external-API configuration and secret-protection abstractions.
- A2: Only transient failures retry.

## Out of scope
Inbound parsing and ticket threading; see US-204.

## Acceptance Criteria
- AC-203.1: Given valid provider configuration, when a message is sent, then the provider receives the email.
- AC-203.2: Given a transient failure, then bounded backoff retries occur.
- AC-203.3: Given a permanent failure, then a safe envelope failure is returned and no false success is recorded.

## Design
Application owns `IEmailSender`; Infrastructure owns the adapter, timeout, retry policy, and secret handling. Original story: `EPIC-03-US-203-email-provider.md` / AC-11.5.
