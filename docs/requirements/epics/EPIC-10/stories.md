# EPIC-10 · Integrations — Stories (All Slices)

| Epic | Slice(s) | BRD Requirements |
|---|---|---|
| `EPIC-10` | S1, S5, S9, Deferred | FR-11.1–FR-11.9, INT-1–INT-10 |

---

## S1 — Own API as Product Surface (Done)

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-101 | Uniform response envelope | Backend | S1 | P0 | `done` | FR-11.1, FND-1–FND-5 |
| US-122 | Stable code per condition (one envelope, one code) | Backend | S1 | P0 | `done` | FR-11.1, AC-51 |
| US-123 | Diagnosable without leaking (traceId, no stack traces) | Backend | S1 | P1 | `done` | FR-11.4, AC-52, AC-53 |
| US-124 | Unambiguous wire format (ISO 8601 UTC, camelCase) | Backend | S1 | P1 | `done` | FR-11.1, AC-54 |
| US-111 | API documents itself truthfully (OpenAPI + health) | Backend | S1 | P1 | `done` | FR-11.2, FND-30–FND-32 |
| — | API auth same as UI (bearer token, INT-2) | Backend | S1 | M | `done` | FR-11.3, INT-2 |

---

## S5 — Email Provider Integration

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| US-203 | Configure email provider (SendGrid / MailKit) | Backend | S5 | M | `not started` | FR-11.5, DEP-1 |
| — | Outbound email with retry + backoff | Backend | S5 | M | `not started` | INT-7 |
| US-204 | Inbound email ingestion (idempotent, INT-6) | Backend | S5 | M | `not started` | FR-11.5, INT-6 |
| — | Failing provider degrades feature, not app (INT-10) | Backend | S5 | M | `not started` | INT-10 |

---

## S9 — Webhooks & Corporate Identity

| Story | Title | Layer | Slice | Priority | Status | FR |
|---|---|---|---|---|---|---|
| — | Outbound webhooks (signed payload, event ID) | Backend | S9 | S | `not started` | FR-11.6, INT-9 |
| — | Corporate identity provider auth (SSO/SAML/OIDC) | Backend | S9 | C | `not started` | FR-11.7 |

---

## Deferred (BRD §6.3)

| Story | Title | Status | Reason |
|---|---|---|---|
| FR-11.8 | ERP data exchange | Deferred | No named ERP (OQ-9, DEP-7) |
| FR-11.9 | WhatsApp/SMS provider integration | Deferred | Paid providers, no staffing |

---

## Summary

| Slice | Total Stories | Done | Not Started |
|---|---|---|---|
| S1 | 6 | 6 | 0 |
| S5 | 4 | 0 | 4 |
| S9 | 2 | 0 | 2 |
| Deferred | 2 | — | — |
| **Total** | **12** | **6** | **6** |
