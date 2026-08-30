# Assumptions and open questions

The register the rule specification §13 requires. **Ownership is unchanged**: each identifier stays
owned by the document that created it — `B-n` by [`../assessment/brief.md`](../assessment/brief.md),
`A-n` by the ticket-lifecycle spec, `PA-n`/`OQ-n`/`G-n`/`RSK-n`/`DEP-n` by the
[BRD](../brd/customer-support-crm-brd.md). This file does not restate their content; it indexes
it and adds only what the rule specification's discovery list raises that no existing register
holds (`OQ-11`, `OQ-12`, numbered continuing the BRD sequence).

## Assumptions in force

| Register | Items | Governs |
|---|---|---|
| Brief | `B1`–`B5` | Product reading: vision-not-deliverable, depth-over-breadth, no staff self-registration, grouping-not-tenancy, AI deferred-not-stubbed |
| Ticket-lifecycle spec (`A-n`) | `A1`–`A10` | Slice S1 behaviour — see that spec's assumptions section |
| Backend-foundation spec | its own assumption list | Response envelope, catalogue, persistence conventions |
| BRD product assumptions | `PA-1`–`PA-11` | Business-hours calendars, email-as-identity, CSAT shape, slice placements, branch scoping, Arabic placeholder copy, reporting thresholds |

The three most likely wrong, per the BRD itself: `PA-4`, `PA-5`, `PA-11` — each supplies a position
the brief does not take.

## Open question register

### Carried from the BRD (authoritative there)

| Id | Question (abridged) | Blocks | Status |
|---|---|---|---|
| `OQ-1` | Merge duplicate customer records? | `PA-2`, S9 scope | Open |
| `OQ-2` | Actual SLA targets per priority | `DEP-3`, S2 | Open — answer first |
| `OQ-3` | 24/7 or business hours? | S2, all duration KPIs | Open — answer first |
| `OQ-4` | Auto-close resolved tickets after silence? | S2, `KPI-2` | Open |
| `OQ-5` | Do branches restrict visibility or only group? | S8, `FR-9.8` | Open |
| `OQ-6` | 7-year audit vs 24-month ticket retention acceptable? | Data minimisation | Open |
| `OQ-7` | How is data-subject erasure satisfied under soft delete? | `RSK-9`, compliance | Open |
| `OQ-8` | Which model provider, on what data-processing basis? | S7, `DEP-6` | Open |
| `OQ-9` | Named ERP, or aspirational? | `DEP-7` | Open |
| `OQ-10` | Who reviews Arabic copy, when available? | `FR-12.5`, `BO-6` | Open |

Full text and owners: [BRD §21](../brd/customer-support-crm-brd.md#21-open-questions).

### Raised by the rule specification's discovery lists — new here

| Id | Question | Blocks | Asked of | Status |
|---|---|---|---|---|
| `OQ-11` | Does an inbound email create a new ticket or update an existing one — and how is customer identity matched to a thread? The rule spec §8 (EPIC-03) requires this be specified before any channel work | S5 sprints 6 & 9 | Product owner | Open |
| `OQ-12` | What is the approved automatic-assignment algorithm — round-robin, least workload, skills, category, branch? What happens when no agent is available? (rule spec §12) | S2 auto-assignment story | Support management | Open |

### Questions the rule specification poses that are already answered here

| Rule-spec question | Answered by |
|---|---|
| Can one ticket have multiple agents? (rule OQ-003 seed) | **No** — `BR-2`: at most one assignee at any time |
| What are the allowed ticket statuses and transitions? (rule OQ-009 seed) | The transition table in the ticket-lifecycle spec; `BR-3`/`BR-4`; undefined jumps are state conflicts (409) |
| Is AI required for MVP? (rule OQ-005 seed) | No — `B5`: deferred, never stubbed; core CRM must work without it |
| Which WhatsApp provider? (rule OQ-001 seed) | Deferred indefinitely with reason — BRD §6.3; revisit only if the channel is reopened |
| Which roles are required? (rule OQ-008 seed) | Two seeded for S1 (`Admin`, `Agent`) per `B3`; remainder proposed as slice S9 — `G-2` |
| What customer-satisfaction process? (rule OQ-010 seed) | Shape fixed by `PA-3` (one 1–5 question + optional text); delivery in S3 |
| Is multi-tenancy required? (rule OQ-006 seed) | No — `B4`: organisational grouping, not tenant isolation |

## Gaps against the brief

`G-1`–`G-4` live in [BRD §22](../brd/customer-support-crm-brd.md#22-gaps-and-conflicts-raised-against-the-brief)
and remain open: area 4 features with no slice (`G-1`), area 10's remainder including the system
audit log proposed as S9 (`G-2`), S2 depending on S5's message record — resolved in sequencing by
pulling it to sprint 6 (`G-3`), and the corrected criteria count in the traceability document
(`G-4`).
