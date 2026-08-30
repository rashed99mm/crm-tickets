# ADR 0009 — Adopt the CCE Platform reference as the CRM backend baseline

- **Status:** Accepted
- **Date:** 2026-08-25

## Context

With roughly a day and a half of a three-day budget left, the from-scratch build had produced a
response envelope, a bilingual message catalogue, domain base types, soft delete, authentication and
staff administration — around 260 passing tests — but **no feature an assessor would recognise as a
support CRM**. No customers, no tickets, no queue. The documentation-to-code ratio had reached
roughly 4:1.

Two of the nine graded criteria (Backend, Frontend) require working features, a third
(Correctness) needs something to be correct about, and a fourth (Productivity) plausibly measures
throughput. That is around 40 of 100 marks resting on breadth the from-scratch path was not going to
reach in the time left.

A working platform existed in `refrence/support-platform` — the same house pattern ADR-0004 already cites
for the response envelope. Inspected before any decision: it **built with 0 errors and passed 97
tests**, and carried Auth, Users, Contents, Notifications, PlatformSettings and
ExternalApiConfigurations, plus localisation, auditing, messaging and migrations.

## Decision

Adopt that platform as the backend baseline. Copy it in, rename the inherited namespaces to `CustomerSupport.*`, split
its single API host into an internal staff host and an external customer host over a shared
composition core, and add the ticket workflow it lacks.

Domain naming stays the brief's: `CustomerSupport.*`, because `brief.md`, the BRD and every
requirements document already say so and an assessor reads those first.

## Alternatives considered

| Option | Why it lost |
|---|---|
| Keep building from scratch | The honest projection was more infrastructure and still no tickets, customers or queue by the deadline. It was losing on exactly the criteria that need running software. |
| Hybrid: keep the tested code, adopt the rest | Genuinely attractive — nothing proven is discarded. Rejected because merging two conventions (`Response<T>` versus `Result<T>`, two message catalogues, two validation pipelines) is itself a day of work, and the likely result is a half-and-half codebase that is harder to explain than either half. |
| Restructure the existing code into the reference shape | Delivers no new features. It only changes the shape of what already fell short. |
| Ship the reference under its original naming | Least renaming, but it contradicts every requirements document in the repo. |

## Consequences

**Bought:** six working feature areas, Arabic/English localisation on every response, audit entities,
MassTransit messaging, Hangfire jobs, SignalR hubs, a migration history, and 97 tests — in about an
hour of adaptation rather than days of construction.

**Paid:** the hand-built auth, envelope, message catalogue and S1 domain entities are gone, and with
them roughly 260 tests. They are **archived, not deleted**
(`scratchpad/pre-reference-backup/backend-and-frontend.zip`, 218 files, contents verified). The
adopted code is covered by 97 tests that test the reference's concerns, not the brief's — so test
*count* went down and, more importantly, so did the proportion of the brief that tests actually
cover. That is the real cost and it should not be described as a win.

**Now harder:**

- The Angular frontend expects `Response<T>` with `{ success, code, message: { ar, en } }`; the
  platform returns `Result<T>` with `{ isSuccess, error: { code, messageAr, messageEn } }`. One
  interceptor bridges it, which is the payoff for having confined envelope knowledge to one file.
- Specs `AC-n`, `FND-n` and `AUTH-n` describe code that no longer exists. The criteria remain valid
  as *requirements*; their delivered status does not, and every affected story has been reset rather
  than left claiming work that was replaced.
- The reference carries dependencies this project did not choose — Redis, RabbitMQ, Hangfire, Seq.
  None is needed to serve a request, but they are now in the dependency graph.

**Reversible?** Partly. The archive makes the old backend recoverable, but anything built on the new
baseline would have to be rewritten. Treat this as a one-way door.

**One correction worth recording.** The first diagnosis of the post-adoption outage blamed those
inherited dependencies. It was wrong: the cause was a missing `Jwt:Key`, and the exception middleware
sitting first in the pipeline turned that single misconfiguration into a 500 for every request,
including the OpenAPI document. Guessing at infrastructure cost time that reading a log would not
have — and the log was invisible because the inherited Serilog configuration has no console sink.
