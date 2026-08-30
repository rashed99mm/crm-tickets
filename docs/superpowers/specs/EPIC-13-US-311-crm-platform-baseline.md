# CRM platform baseline — adapted from the CCE Platform reference

**Date:** 2026-08-25
**Criterion ids:** `BASE-n`. Separate prefix; does not collide with `AC-n`, `FND-n` or `AUTH-n`.
**Source:** `refrence/cce-platform` — the same house pattern ADR-0004 already cites for the response
envelope.

## Why this document exists, and what it is not

The delivery approach changed. Building the CRM feature-by-feature from an empty solution was too
slow against the remaining deadline, so a working platform was adopted as the baseline and renamed
into the CRM domain instead.

**This spec documents an adapted baseline; it is not a spec written before its code.** That
distinction matters because `CLAUDE.md` forbids the reverse order, and pretending otherwise would be
worse than saying it. What it *does* establish is the line from which the normal rule resumes: every
change after this document gets its spec first, and the git timestamps show that.

## What was adopted

The reference is a Clean Architecture DDD monolith that **builds clean and passes 97 tests**. Its
surface, renamed `CCE.*` → `CustomerSupport.*`:

| Reference feature | Role in the CRM | State |
|---|---|---|
| `Auth` | Staff sign-in, JWT, refresh | Inherited, working |
| `Users` | Staff accounts and roles | Inherited, working |
| `Contents` | **The knowledge base** — help articles with author, status, publish lifecycle | Inherited, working |
| `Notifications` | Alerts to staff | Inherited, working |
| `PlatformSettings` | System configuration without a deployment | Inherited, working |
| `ExternalApiConfigurations` | The integrations surface (brief area 11) | Inherited, working |
| `Audit` entities | Accountability trail (`BO-7`) | Inherited |
| Localization | Arabic/English on every response | Inherited |
| SignalR hubs | Real-time push | Inherited, inert until configured |
| Migrator + 2 migrations | Schema delivery | Inherited, applied |

**Not yet present, and the real remaining work:** the ticket workflow. Tickets, status machine,
assignment, ticket history — brief areas 1, 2 and 4 — have no counterpart in the reference and are
what `BASE-9` onward covers.

## Project layout

Seven projects. Two API hosts over one shared composition core, which is ADR-0008's shape:

```
src/
  CustomerSupport.Domain/              entities, value objects, events, specifications
  CustomerSupport.Application/         features (CQRS via MediatR), contracts, behaviors
  CustomerSupport.Infrastructure/      EF Core, Identity, messaging, jobs, localization
  CustomerSupport.Shared.Contracts/    message contracts shared with consumers
  CustomerSupport.Api.Shared/          composition core: extensions, middleware, hubs, config
  CustomerSupport.InternalApi/         staff host   — full surface, seeds on start
  CustomerSupport.ExternalApi/         customer host — narrow, read-only, no seeding
  CustomerSupport.Migrator/            schema tool
tests/
  CustomerSupport.Tests/               97 inherited tests
```

`Api.Shared` exists because both hosts must answer in an identical envelope with an identical
pipeline order. Two copies of that wiring would drift, and the drift would be a customer-facing host
answering in a shape the staff host does not.

## Acceptance criteria

Priority: **P0** must hold, **P1** should.

### The adapted baseline

- **BASE-1** (P0) The solution builds with **0 errors and 0 warnings**.
- **BASE-2** (P0) All inherited tests pass — **97 of 97**.
- **BASE-3** (P0) No identifier, namespace, project or database name carries the reference's `CCE`
  branding. Verified by search, not by inspection.
- **BASE-4** (P0) Domain, Application, Infrastructure and the two hosts keep dependencies pointing
  inward. `Domain` references no other project.

### Two hosts

- **BASE-5** (P0) `InternalApi` exposes the staff surface: auth, users, content authoring,
  notifications, platform settings, integration configuration.
- **BASE-6** (P0) `ExternalApi` exposes **only** published knowledge-base articles, read-only and
  anonymous. Authoring, accounts, settings and integration configuration are absent from that host —
  not merely authorized away.
- **BASE-7** (P0) The external host does **not** seed data on start-up. Seeding is administrative
  and belongs to the internal host.
- **BASE-8** (P0) Both hosts share one composition core, so envelope, pipeline order and
  serialization cannot diverge.

### XML documentation

- **BASE-9** (P0) Every project generates an XML documentation file.
- **BASE-10** (P1) Public controllers and their actions carry `<summary>`, and parameters carry
  `<param>`, so the published API document explains itself without the source.

### The ticket workflow — the remaining gap

- **BASE-11** (P0) A `Ticket` aggregate exists with reference, subject, customer, category,
  priority, status and assignee.
- **BASE-12** (P0) Status changes follow a closed transition table held in the domain entity, and
  any other transition is refused as a state conflict rather than a validation error.
- **BASE-13** (P0) Assignment is a supervisor action; an agent may progress only their own ticket.
- **BASE-14** (P0) Every creation, assignment and status change appends an append-only history row
  recording actor, UTC timestamp and the from/to values.

`BASE-11`–`BASE-14` restate `AC-29`–`AC-50` from the ticket-lifecycle spec, which remains the
authority on their detail. They appear here so this document is not read as claiming the workflow
already exists.

## Runtime configuration — required, and previously the cause of a total outage

Both hosts need two settings. Without the second, **every request returned 500**, including
`/openapi/v1.json`:

| Setting | Value used locally |
|---|---|
| `ConnectionStrings:DefaultConnection` | `Server=(localdb)\MSSQLLocalDB;Database=CustomerSupportCrm;Trusted_Connection=True;TrustServerCertificate=True` |
| `Jwt:Key` | any key of sufficient length |

**Corrected 2026-08-25.** An earlier version of this document diagnosed that outage as the
reference's Redis, RabbitMQ, Hangfire and Seq dependencies. **That was wrong.** The actual cause was
a missing `Jwt:Key`: `AddPlatformAuthentication` throws
`"JWT authentication is enabled but no valid Jwt configuration was found"`, and because the
exception middleware sits first in the pipeline it converted that into the envelope for every
request — which is why even the OpenAPI document 500ed and made it look like an infrastructure
problem. Redis and RabbitMQ turned out not to be needed to serve requests at all.

The diagnosis was only found by overriding Serilog to a console sink, because the inherited logging
configuration writes nowhere visible by default. That is worth knowing before the next outage.

**BASE-10 needed work that the reference did not have.** Every project generated an XML file and
none of it reached the served document. `XmlDocumentationTransformer` in `Api.Shared` now copies
`<summary>` to the operation summary, `<remarks>` to the description, and `<param>` to parameter
descriptions. Verified: **35 documented operations on the internal host, 2 on the external host.**

## Verified running

| Check | Result |
|---|---|
| Internal host | 30 paths, 35 operations carrying XML prose |
| External host | 3 paths, published-articles endpoint returns a paginated envelope |
| `GET /health` | 200 |
| `GET /api/contents` | 200 |
| `GET /api/users` without a token | **401** |
| `POST /api/Auth/login` with the seeded administrator | 200, 676-character JWT |
| `GET /api/users` with that token | **200** |

## Assumptions

- **R1.** The reference's features map onto the brief as tabled above; `Contents` is the knowledge
  base (brief area 6), not a generic CMS surface.
- **R2.** Adopting the reference discards the previously hand-built auth, envelope, message catalogue
  and S1 entities. That code and its ~260 tests are archived, not deleted, and are recoverable.
- **R3.** The reference's infrastructure dependencies are a runtime concern, not an architectural
  one. If the stack cannot run here, the affected features are documented as unverifiable rather
  than reported as working.

## Out of scope

The Angular frontend, which still targets the previous API contract and will need re-pointing ·
Playwright coverage · the SLA, portal, AI and reporting slices · replacing the reference's
`Result<T>` contract with the earlier `Response<T>` envelope, since the reference's is the one the
inherited 97 tests cover.
