# US-124 · The wire format is unambiguous

| Field | Value |
|---|---|
| **Story** | `US-124` *(was `US-1.35`)* |
| **Epic** | [EPIC-12 Platform features](../epics/EPIC-12-platform.md) |
| **Feature** | [`FEAT-09` Contract hardening](../delivery-plan.md#feat-09--contract-hardening) |
| **Layer** | Backend |
| **Ships with** | — API-only. Cross-cutting hardening proven across the endpoints the earlier features built. |
| **Rule proposal** | — appended number; no rule-file counterpart |
| **Actor** | Internal — Frontend developer |
| **Priority** | P1 |
| **Sprint** | [4 — Contract hardening, localisation and the journey](../delivery-plan.md#sprint-4--contract-hardening-localisation-and-the-journey) · Slice S1 |
| **Estimate** | 2 points |
| **Status** | `done` |
| **BRD requirements** | NFR-16, BR-23 |
| **Spec criteria** | AC-54 |
| **Depends on** | [US-101](./US-101-uniform-response-envelope.md) *(sprint 1)* |

## Story

**As a frontend developer**, **I want** predictable date and property conventions, **so that** I do not write per-endpoint mapping code.

## Business rules

- BR-23 — timestamps stored/transmitted UTC, rendered in reader's timezone (BRD).

## Acceptance criteria

Criteria are cited from the spec, not paraphrased. The spec is authoritative; if this file and the
spec disagree, the spec is right and this file is stale.

#### AC1 — ISO 8601 UTC, camelCase (spec AC-54)

Dates on the wire are ISO 8601 UTC; JSON properties are camelCase.

## SQL tables

None directly. The storage side of the rule is that every timestamp column is `DATETIMEOFFSET`
(UTC) — see [S1 schema](../../superpowers/specs/EPIC-12-US-000-s1-schema.md) — which is what makes
ISO 8601 UTC on the wire a projection rather than a conversion.

## Test cases

| # | Criterion | Level | Test | Given / When / Then | Expected |
|---|---|---|---|---|---|
| TC-01 | AC-54 (camelCase half) | Api.IntegrationTests | ✅ `HealthEndpointTests.Property_Names_Are_CamelCase` | a real response / inspect keys | camelCase; `TraceId` absent |
| TC-02 | AC-54 (date half) | Api.IntegrationTests | `planned` — unblocks when the first date-bearing DTO ships (US-001) | a created customer / inspect `createdAtUtc` on the wire | ISO 8601 UTC string |

## Notes

UTC on the wire and rendering in the reader's timezone is the only arrangement that survives a user changing timezone, and camelCase is what makes US-127's field-to-control mapping a lookup rather than a translation.

## Open questions

None.

## Status evidence

Verified rather than re-implemented - the story is `superseded` as written (it described the
hand-built envelope), but AC-54 stands.

AC-54 -> `AC54_ResponseProperties_AreCamelCase` and `AC54_DatesOnTheWire_AreIso8601Utc`.

**The date half was broken.** `createdAt` went out as `2026-08-25T22:58:48.9296923` - ISO 8601 in
shape, with **no timezone designator**. Entities store `DateTime.UtcNow`, but EF returns
`DateTimeKind.Unspecified` after a round trip so the serializer wrote no `Z`. Every browser parses
that as local time: invisible on this machine, three hours wrong for an agent in Cairo.

Fixed by `UtcDateTimeConverter` (and its nullable overload - a `DateTime?` bypasses the
non-nullable converter entirely). Now: `2026-08-25T23:03:42.478Z`.

Run 2026-08-26: 242 passed, 0 failed.

Status is set from what is committed and executed, never from what is planned. See
[the conventions](../README.md#status-vocabulary).
