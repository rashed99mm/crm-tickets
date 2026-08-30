# EPIC-12 · Platform features

| | |
|---|---|
| **Epic** | `EPIC-12` |
| **Priority** | P0 (S1 share) |
| **Stories** | 15 specified · 216-point plan share: 57 pts |
| **Sprints** | 1 (foundation) · 3 (API contract) · 4 (language switching) |
| **Criteria** | `FND-1`…`FND-32a`, AC-51…AC-54, AC-63, AC-68 — see [`../slice-s1-coverage.md`](../slice-s1-coverage.md) |

## Goal

Provide platform-level capabilities required across the system *(rule specification §8)*. S1
delivers the invisible half of this epic — the response contract, bilingual message foundation,
persistence conventions, and build-enforced architecture that every later feature stands on.

## Why this epic exists

Nothing here is visible to a support agent, and nothing after it can be built without it. Its
requirements are the ones most often assumed and least often tested: one response shape so clients
write one handler (`FND-1`); the outcome-to-status mapping in exactly one place so a new failure
type cannot acquire its own status by accident; messages in Arabic and English **before the
application will start**, because graceful degradation ships blank strings; deletion that is soft
and uniqueness that survives it (`BR-8`, `BR-9`, ADR 0006); and the dependency rule enforced by the
build rather than by review, because it is the single claim an assessor can check mechanically.

## Stories

| Story | Title | Priority | Points | Status | Criteria |
|---|---|---|---|---|---|
| [US-093](../user-stories/US-093-bilingual-instant-switching.md) | The interface is bilingual and switches instantly *(rule proposal: Change Application Language; Arabic/English interfaces)* | P1 | 5 | `not started` | AC-63, AC-68 |
| [US-101](../user-stories/US-101-uniform-response-envelope.md) | Uniform response envelope | P0 | 5 | `partial` | FND-1, FND-2, FND-3, FND-5 |
| [US-102](../user-stories/US-102-outcome-to-status-mapping.md) | One place decides the HTTP status | P0 | 3 | `done` | FND-4, FND-8 |
| [US-103](../user-stories/US-103-trace-id-and-timestamp.md) | Every response is traceable to its log line | P1 | 3 | `partial` | FND-6, FND-7 |
| [US-104](../user-stories/US-104-field-keyed-validation-errors.md) | Validation failures arrive keyed to their field | P0 | 5 | `done` | FND-9…FND-11 |
| [US-105](../user-stories/US-105-reflection-free-validation-pipeline.md) | The validation pipeline runs without reflection, and is proven to run | P0 | 3 | `done` | FND-12, FND-13, FND-13a |
| [US-106](../user-stories/US-106-bilingual-message-catalogue.md) | Messages are bilingual and loaded once at startup | P0 | 5 | `done` | FND-14…FND-17, FND-20 |
| [US-107](../user-stories/US-107-every-code-has-a-message.md) | The build fails when a code has no message | P0 | 3 | `done` | FND-18, FND-19, FND-21 |
| [US-108](../user-stories/US-108-domain-base-types.md) | Domain base types with identity and component equality | P1 | 3 | `done` | FND-22, FND-27, FND-28 |
| [US-109](../user-stories/US-109-auditing-and-soft-delete.md) | Auditing and soft delete happen without being asked | P0 | 5 | `done` | FND-23…FND-26 |
| [US-110](../user-stories/US-110-dependency-rule-enforced.md) | The dependency rule is enforced by the build | P0 | 2 | `done` | FND-29 |
| [US-111](../user-stories/US-111-api-documentation-and-health.md) | The API documents itself truthfully and reports its health | P1 | 5 | `partial` | FND-30…FND-32 |
| [US-122](../user-stories/US-122-stable-code-per-condition.md) | One envelope and one stable code per condition | P0 | 5 | `not started` | AC-51, AC-66 |
| [US-123](../user-stories/US-123-diagnosable-without-leaking.md) | Failures are diagnosable without leaking anything | P1 | 3 | `not started` | AC-52, AC-53 |
| [US-124](../user-stories/US-124-unambiguous-wire-format.md) | The wire format is unambiguous | P1 | 2 | `partial` | AC-54 |

Absorbs former epics `EP-1.01` Platform foundation, `EP-1.08` API contract, plus US-1.41's language
mechanism.

## Reserved backlog (unspecified — titles only, no fabricated rules)

| Rule proposal | Future home | Blocked on |
|---|---|---|
| US-094 Use Arabic Interface / US-095 Use English Interface | realized by US-093's mechanism + reviewed copy | reviewed Arabic — `DEP-5`, `OQ-10`; placeholders until then (`PA-7`) |
| US-096 Configure Department · US-097 Configure Branch · US-098 Access Department-Specific Data | sprint 7 (organisational structure) | `OQ-5`, `PA-11`, `RSK-7` — dimension history cannot be retrofitted |
| US-099 Configure Custom Branding | sprint 14 | — |
| US-100 Use Mobile-Friendly Interface | responsive web throughout; explicit targets at sprint 14 (`NFR-15`) | native apps ruled out — brief ambiguity ruling |
