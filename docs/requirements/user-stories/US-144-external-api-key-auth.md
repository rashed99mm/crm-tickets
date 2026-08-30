# US-144 · API-key authentication on ExternalApi

| Field | Value |
|---|---|
| **Story** | `US-144` |
| **Epic** | [EPIC-10 Integrations](../epics/EPIC-10-integrations.md) |
| **Feature** | `FEAT-24` API-key auth |
| **Layer** | Backend |
| **Ships with** | — |
| **Actor** | Machine client (external system) |
| **Priority** | P1 |
| **Estimate** | 3 points |
| **Status** | `not started` |
| **BRD requirements** | FR-11.1 |
| **Spec criteria** | AC-144 |
| **Depends on** | — |

## Story

**As a machine client**, **I want** to authenticate with an API key, **so that** I can read the public surface of the CRM (knowledge base articles, ticket submit by reference) without a user session.

## Business rules

- BR-144.1: API keys are stored in configuration (user-secrets), never in the database.
- BR-144.2: Key comparison must be constant-time to prevent timing attacks.
- BR-144.3: A missing or invalid key returns a 401 with the standard error envelope, never a raw 401.
- BR-144.4: API-key auth is additive to JWT — existing anonymous and JWT-authenticated endpoints are unchanged.

## Acceptance criteria

#### AC-144.1 — Valid key grants machine access

Given a valid API key in the `X-Api-Key` header, when a request is made to a protected endpoint, then the request is processed normally and the response is returned.

#### AC-144.2 — Invalid or missing key gets 401 envelope

Given no API key or an invalid API key in the `X-Api-Key` header, when a request is made, then the response is `401 Unauthorized` with the standard error envelope (matching the contract-hardening envelope shape).

#### AC-144.3 — Scoped to ExternalApi surface

Given an API key, when a request is made to an InternalApi endpoint, then the key is not accepted (InternalApi requires JWT).

## SQL tables

None — configuration-based.

## Test cases

| # | Criterion | Level | Test | Given / When / Then |
|---|---|---|---|---|
| TC-01 | AC-144.1 | Integration | `ValidKeyGrantsMachineAccess` | Given a valid `X-Api-Key`, when GET /api/knowledge-base/articles is called, then 200 with articles |
| TC-02 | AC-144.2 | Integration | `MissingKeyGets401Envelope` | Given no `X-Api-Key`, when a request is made, then 401 with envelope |
| TC-03 | AC-144.2 | Integration | `InvalidKeyGets401Envelope` | Given an invalid `X-Api-Key`, when a request is made, then 401 with envelope |
| TC-04 | AC-144.3 | Integration | `ApiKeyRejectedOnInternalApi` | Given a valid `X-Api-Key`, when GET /api/tickets is called on InternalApi, then 401 or 403 |

## Notes

- The key is configured via user-secrets or environment variable (`ExternalApi__ApiKey`), not in the database.
- Constant-time comparison: use `CryptographicOperations.FixedTimeEquals` or equivalent.
- Only the public surface (KnowledgeBase, Portal, Categories, ticket submit by reference) accepts API-key auth on ExternalApi.

## Open questions

None.

## Status evidence

Not yet implemented.
