# Personas and actors

The rule specification §5 defines seven actors. This product's implemented reality (S1) has two
staff roles seeded — **Admin** and **Agent** — plus a supervisor/manager distinction that is
behavioural (per-record authorization, `BR-11`) rather than a third role today. Personas in prose
are from BRD §5.2; the mapping below is ours.

## Rule-specification actors → this system

| Actor | In this system | Today (S1) | Later |
|---|---|---|---|
| **Customer** | The person raising requests. Portal-facing | Not yet an authenticated actor; appears as ticket data only | S3 portal |
| **Support Agent** | Seeded role `Agent`. Works assigned tickets; may progress only their own (`BR-11`) | ✅ S1 | |
| **Team Lead** | The "supervisor" of our stories and BRD persona: assigns work, sees any ticket, deletes guarded records | Behaviour of role `Admin`'s elevated permissions + `BR-10`; a distinct *role* is not seeded in S1 | S9 user management |
| **Support Manager** | The BRD's department manager: reads reports, owns SLA attainment | Not yet present | S2, S6 |
| **Administrator** | Seeded role `Admin`: creates staff accounts, assigns tickets, configures categories/priorities | ✅ S1 (accounts seeded administratively per `B3`) | S9 |
| **System** | SLA calculation, automation, notifications, audit recording | Audit fields via persistence interceptor (`FND-23`); the rest later | S2+ |
| **AI Assistant** | Optional intelligence layer; core CRM must work without it (rule spec §17.1) | Out of scope, deferred not stubbed (`B5`) | S7, gated on `OQ-8` |

## Internal actors (addition beyond §5)

Several S1 stories are written from the perspective of the people who must *operate or trust* the
system rather than act inside it. These are legitimate stakeholders (BRD §5.1) but are not business
actors, so stories label them `Internal — …`:

| Label | Who | Stories exist because |
|---|---|---|
| `Internal — API consumer` | Frontend developers and future integrators | One response envelope, stable codes |
| `Internal — Maintainer` | The next developer | Single mapping points, reflection-free pipelines, base types, build-enforced rules |
| `Internal — Support engineer` | Whoever gets paged | Correlation ids, timestamps |
| `Internal — Security reviewer` | Review accountable for authz and data protection | No user enumeration, no credential leakage, confined uploads |
| `Internal — Data protection owner` | BRD §5.1 row 9 | Soft delete, attribution, retention |
| `Internal — Product owner` | The client voice | Bilingual messages fail at startup, not in front of a customer |
| `Internal — Integration owner` | BRD §5.1 row 8 | Truthful OpenAPI, health endpoint |
| `Internal — Reviewer` | Assessor / reviewer of this work | End-to-end journey proof |

## Persona prose (from BRD §5.2)

- **The customer** — contacts support two or three times a year; wants to know the request was
  received and roughly when it will be answered; reads in Arabic or English; their pain is silence.
- **The agent** — works a queue for a full shift; needs the customer's history beside the request;
  pain is duplicated work and requests they were never told were theirs.
- **The supervisor** — answers for the queue rather than working it; needs what is outstanding, who
  has capacity, what is about to breach; pain is that every answer requires asking a person.
- **The department manager** — reads numbers weekly, challenged monthly; needs figures computed the
  same way every time with a stated definition.
