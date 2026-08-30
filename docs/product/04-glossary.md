# Glossary

Owned by the BRD §20 (bilingual, with the caveat that Arabic terms there are shared vocabulary, not
reviewed interface copy — `PA-7`). This file indexes the terms an engineer or assessor meets most
often, plus the S1-specific vocabulary the BRD does not own. **The BRD table is authoritative for
definitions; this file adds only system-specific terms.**

## Product terms

Ticket · Customer · Agent · Supervisor · Assignment · Status · Priority · Category · Escalation ·
SLA · First response · Resolution · Reopen · Ticket history · Interaction history · Knowledge base ·
Deflection · Backlog · CSAT · Branch · Department · Audit log — defined bilingually in
[BRD §20](../brd/customer-support-crm-brd.md#20-glossary).

## System terms introduced by the specs

| Term | Meaning | Owner |
|---|---|---|
| Envelope | The single response shape `{ success, code, message: { ar, en }, data, errors[], traceId, timestamp }` returned by every endpoint | Backend-foundation spec `FND-1`; ADR 0004 |
| System code | Stable machine-readable code (`CON…`, `ERR…`, `VAL…`) carried by every envelope; meaning never changes, new meaning gets a new code | `FND-14`, US-122 |
| Message catalogue | One flat YAML file keyed by domain key, each entry carrying `ar` and `en`, parsed once at startup; malformed content fails startup | `FND-15`–`FND-17`; ADR 0007 |
| Transition table | The permitted status edges living in the `Ticket` entity; any other transition is a **state conflict** (409), not a validation error | `BR-3`, ticket-lifecycle spec |
| State conflict | Request well formed, state wrong — 409 naming the rule. Contrast validation error (400) | `AC-38`, US-118 |
| Soft delete | Deletion marks a row deleted and retains it; global query filter excludes it; unique indexes are filtered so freed values are reusable | ADR 0006; `FND-24`–`FND-26` |
| Audit stamping | `CreatedAt/By`, `ModifiedAt/By` written by the persistence interceptor, never by handlers | `FND-23` |
| Row version | Optimistic-concurrency token on `Tickets`; a lost update is refused with 409, never silently overwritten | `BR-13` |
| Ticket reference | Human-readable unique stable identifier ("ticket 4192" is not read aloud to a customer) | `BR-15` |
| Slice | Unit of specification (S1–S9). Sprint is the unit of delivery; the two numbers differ by design | [`../requirements/delivery-plan.md`](../requirements/delivery-plan.md) |
