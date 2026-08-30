# System overview

> **Amended 2026-08-25.** The backend is the adapted CCE Platform
> ([ADR-0009](../adr/0009-adopt-the-support-platform-as-the-crm-baseline.md)): eight projects, two hosts
> (`CustomerSupport.InternalApi`, `CustomerSupport.ExternalApi`) over `CustomerSupport.Api.Shared`.
> Project names below have been updated; where this file describes endpoints or a response envelope
> from the previous implementation, the platform's `Result<T>` contract is the one in force.


Initial architecture view — written at the start of slice S1, before implementation. It records
what is *decided*, what is *planned*, and what is deliberately *open*. It will be revised when a
decision invalidates it; revisions happen through ADRs in [`../adr/`](../adr/), not by silent edits.

## Context

A single-product internal support CRM: staff sign in, manage customers, raise and work tickets.
Customers themselves have no portal access on the current roadmap (portal is sprint 10, blocked).
External systems exist only as deferred roadmap concerns — no integration is live today.

```text
      Support Agent / Team Lead / Support Manager        (actors — see ../product/03-personas.md)
             │ HTTPS
             ▼
┌─────────────────────────────┐      ┌───────────────────────────┐   ┌───────────────────────────┐
│  Angular 20 SPA             │ HTTP │  InternalApi (internal)      │   │  ExternalApi (external)   │
│  standalone components +    │─────▶│  .NET 10 minimal API      │   │  .NET 10 minimal API      │
│  signals                    │ JSON │  ┌─────────────────────┐  │   │  (own hosting; arrives    │
└─────────────────────────────┘      │  │ Api.Shared          │◀─┼───│▶ with the portal slice)   │
                                     │  │ composition core,    │  │   └────────────┬──────────────┘
                                     │  │ envelope, rate limits│  │                │
                                     │  └──────────┬───────────┘  │                │
                                     └─────────────┼──────────────┘                │
                                                   ▼                               ▼
                                      ┌──────────────────────────┐
                                      │  Application · Domain    │
                                      │  Infrastructure (EF Core)│
                                      └──────────┬───────────────┘
                                                 ▼
                                      ┌──────────────────────────┐
                                      │  Relational database     │
                                      │  (schema: S1 spec)       │
                                      └──────────────────────────┘

     Email / WhatsApp / SMS / ERP / AI providers ── future adapters only (see integrations.md)
```

## Containers

| Container | Technology | Status |
|---|---|---|
| Frontend | Angular 20, standalone components, signals; Node 24 caps the CLI at 20 | Scaffolded after backend foundation |
| InternalApi | .NET 10 minimal API — internal staff surface; independently hosted ([ADR-0008](../adr/0008-two-api-hosts-shared-composition-core.md)) | Composed and health-proven by tests |
| ExternalApi | .NET 10 minimal API — customer surface, own hosting; endpoints arrive with later slices | Host exists; proven by the same tests |
| Api.Shared | Shared host-side library: composition core, envelope middleware, `ICurrentUser`, rate limiting, health mapping | In use by both hosts |
| Database | Relational, schema owned by [`../superpowers/specs/EPIC-12-US-000-s1-schema.md`](../superpowers/specs/EPIC-12-US-000-s1-schema.md) | Tables specified: `customers`, `tickets`, `ticket_history`, `customer_notes`, `customer_attachments` |
| Tests | xUnit + `WebApplicationFactory` (backend); Vitest/Karma + Playwright (frontend) | Backend suite exists: 101 tests passing |

## The dependency rule

The one invariant: dependencies point inward only.

```text
InternalApi / ExternalApi / Api.Shared ──▶ Application ──▶ Domain
            │                                  ▲
            └──▶ Infrastructure ───────────────┘   (implements Application ports; knows Domain)
```

`Domain` references no framework and no persistence package; `Application` never references
`Infrastructure`. This is enforced mechanically in the `.csproj` files — `Domain.csproj` carries no
`ProjectReference` — and proven by test (`FND-29`, story [`US-110`](../requirements/user-stories/US-110-dependency-rule-enforced.md)).

## Cross-cutting decisions already made

These are settled by the S1 specs, not open:

- **Uniform response envelope** — every response carries success flag, data, error, trace id,
  timestamp (`US-101`–`US-104`, `FND-1..11`).
- **Bilingual message catalogue** — Arabic and English for every code; language selection belongs to
  the client (`US-106`, `BR-22`).
- **Reflection-free validation pipeline** — explicit validators, field-keyed errors (`US-104`,
  `US-105`).
- **Auditing and soft delete** — created/at/by columns everywhere; business rows carry
  `is_deleted`; deletes are guards, not removals (`US-109`, `FND-23..26`).
- **Immutable event history** — ticket changes append to `ticket_history`, never update
  (`US-121`, `AC-48..49`).

## Deliberately open

- Concrete database engine and hosting topology — chosen at first migration, recorded as an ADR.
- Logging/metrics providers — `traceId` correlation (`US-103`) is specified; the sink is not.
- Background job mechanism — needed first by SLA automation (sprint 8).
- Deployment story entirely (availability target itself is unagreed — `NFR-AVL-001`).
