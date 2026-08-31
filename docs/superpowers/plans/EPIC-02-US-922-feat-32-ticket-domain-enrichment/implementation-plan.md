# FEAT-32 Ticket Domain Enrichment — Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add resolution discipline, an impact/urgency→priority matrix, tags, and related/duplicate
links to the ticket domain — four vertical slices in cut order, each backend-complete with tests
before the next starts.

**Architecture:** Clean Architecture, four layers. Domain rules live as value objects and aggregate
methods in `CustomerSupport.Domain` (no dependencies); each slice adds a CQRS command/validator/
handler in `CustomerSupport.Application`, an EF configuration + migration in
`CustomerSupport.Infrastructure`, and endpoints on the existing `TicketsController` in
`CustomerSupport.InternalApi`. Refusal shapes are the existing ones only: field-keyed 400 via
FluentValidation or `messages.Validation`, 409 via `MessageType.Conflict`.

**Tech Stack:** .NET 10, MediatR, FluentValidation, EF Core (SQL Server), xUnit +
`WebApplicationFactory`, FluentAssertions.

**Spec:** `docs/superpowers/specs/EPIC-02-US-922-ticket-domain-enrichment.md` (AC-922.x…AC-925.x).
This is the **backend plan**; per the SDD gate (`CLAUDE.md`), the frontend plan for the same
feature is written immediately after backend implementation completes, not in this document.

## Global Constraints

- Dependency rule: `Domain` references nothing; `Application` references `Domain` only. New value
  objects and entities go in `Domain`, never with EF attributes.
- No new failure shapes: malformed input → 400 with field `errors[]`; wrong-state → 409 via
  `MessageType.Conflict`. No new packages.
- Every new message code needs all four registrations: `ApplicationErrors` const → `SystemCodeMap`
  entry → `SystemCode` const → `Resources.yaml` en+ar pair. Free ranges confirmed 2026-08-31:
  `VAL067+`, `ERR080+`, `CON074+`.
- Every test carries `[Trait("AC", "…")]` naming its criterion. Tests are run, output pasted —
  never claimed.
- Build must stay clean under warnings-as-errors: `cd backend && dotnet build CustomerSupport.slnx`.
- Migrations: `dotnet ef migrations add <Name> --project backend/src/CustomerSupport.Infrastructure
  --startup-project backend/src/CustomerSupport.InternalApi` (from the repo root). Both hosts need
  `ConnectionStrings__DefaultConnection` and `Jwt__Key` set or every request 500s (see `CLAUDE.md`).
- Conventional commits, one logical change each, on a `feat/feat-32-ticket-domain-enrichment`
  branch.
- **Deliberate breaking change (spec A10):** the create-ticket contract drops `priority`. Slice 2
  updates every test fixture that sends `priority` in the same commit — grep
  `backend/tests` for `priority = "` and `Priority:` before claiming the slice done.

## File structure (whole feature)

```
backend/src/CustomerSupport.Domain/
  ValueObjects/TicketResolutionCode.cs      slice 1 — five codes, mirrors TicketPriority
  ValueObjects/ResolutionDetails.cs          slice 1 — record carried into ChangeStatus
  ValueObjects/TicketImpact.cs               slice 2 — Low/Medium/High
  ValueObjects/TicketUrgency.cs              slice 2 — Low/Medium/High
  ValueObjects/PriorityMatrix.cs             slice 2 — pure Derive(impact, urgency)
  ValueObjects/TagValue.cs                   slice 3 — normalization + charset/length rules
  ValueObjects/TicketLinkType.cs             slice 4 — RelatedTo/DuplicateOf
  ValueObjects/TicketChangeType.cs           slices 2,3 — add Reprioritized, TagAdded, TagRemoved
  Entities/Tickets/Ticket.cs                 slices 1,2 — resolution fields, reopen count, Reclassify
  Entities/Tickets/TicketTag.cs              slice 3 — standalone child entity (TicketNote pattern)
  Entities/Tickets/TicketLink.cs             slice 4 — standalone entity, factory guards self-link

backend/src/CustomerSupport.Application/
  Features/Tickets/Commands/ChangeTicketStatus/*      slice 1 — resolution fields; slice 4 — duplicate-link check
  Features/Tickets/Commands/CreateTicket/*            slice 2 — impact/urgency replace priority
  Features/Tickets/Commands/ReclassifyTicket/         slice 2 — new command/validator/handler
  Features/Tickets/Commands/AddTicketTag/             slice 3 — new
  Features/Tickets/Commands/RemoveTicketTag/          slice 3 — new
  Features/Tickets/Commands/AddTicketLink/            slice 4 — new
  Features/Tickets/Commands/RemoveTicketLink/         slice 4 — new
  Features/Tickets/Queries/GetTickets/*               slices 2,3 — impact/urgency in projection, tag filter
  Features/Tickets/Queries/GetTicketById/*            slices 1,2,3,4 — DTO enrichment
  Features/Tickets/Dtos/TicketDtos.cs                 all slices — fields appended at the END each slice
  Features/Ai/Chat/AiChatFeatures.cs                  slice 2 — fixes the invalid Priority:"Medium"
  Errors/ApplicationErrors.cs                         all slices — new consts
  Messages/SystemCode.cs, Messages/SystemCodeMap.cs   all slices — new codes

backend/src/CustomerSupport.Api.Shared/Localization/Resources.yaml   all slices — en/ar pairs
backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/
  TicketConfiguration.cs                    slices 1,2 — column config
  TicketTagConfiguration.cs                 slice 3 — new
  TicketLinkConfiguration.cs                slice 4 — new
backend/src/CustomerSupport.InternalApi/Controllers/TicketsController.cs  all slices — endpoints
backend/src/CustomerSupport.ExternalApi/Controllers/PortalController.cs   slice 2 — drop customer-chosen priority

backend/tests/CustomerSupport.Tests/
  Unit/Domain/TicketResolutionTests.cs       slice 1
  Unit/Domain/PriorityMatrixTests.cs         slice 2
  Unit/Domain/TicketReclassifyTests.cs       slice 2
  Unit/Domain/TagValueTests.cs               slice 3
  Unit/Domain/TicketLinkTests.cs             slice 4
  Integration/TicketResolutionEndpointTests.cs      slice 1
  Integration/TicketClassificationEndpointTests.cs  slice 2
  Integration/TicketTagEndpointTests.cs             slice 3
  Integration/TicketLinkEndpointTests.cs            slice 4
```

## Tasks

Each task is one vertical backend slice, one task file, executed in order. **Cut order if time
runs out: Task 4 first, then Task 3** (spec decision). Task 4 also completes AC-925.3, which
slice 1 deliberately leaves open (until links exist, the `Duplicate` code is accepted without a
link — recorded, not hidden).

| # | Task file | Stories / criteria | Delivers |
|---|---|---|---|
| 1 | [`tasks/01-resolution-discipline.md`](tasks/01-resolution-discipline.md) | US-922 · AC-922.1…6 | `TicketResolutionCode`, resolution required on `Resolved`, reopen clears + counts, DTO + endpoint fields, migration `AddResolutionDiscipline` |
| 2 | [`tasks/02-impact-urgency-matrix.md`](tasks/02-impact-urgency-matrix.md) | US-923 · AC-923.1…6 | `TicketImpact`/`TicketUrgency`/`PriorityMatrix`, matrix-only create, `POST /{id}/classification`, portal/AI defaults, migration `AddImpactUrgencyClassification` |
| 3 | [`tasks/03-ticket-tags.md`](tasks/03-ticket-tags.md) | US-924 · AC-924.1…4 | `TicketTag` + `TagValue`, add/remove endpoints, history rows, `tag=` queue filter, migration `AddTicketTags` |
| 4 | [`tasks/04-ticket-links.md`](tasks/04-ticket-links.md) | US-925 · AC-925.1…5 (API half) + AC-925.3 closing AC-922's gap | `TicketLink` + guards, link endpoints, `links[]` on detail, Duplicate-code⇔link rule, migration `AddTicketLinks` |

Frontend criteria AC-922.7, AC-923.7, AC-924.5 (screen half) and AC-925.5 (screen half) belong to
the frontend plan, written when these four tasks are implemented and green.

## Verification gate (every task)

1. `cd backend && dotnet build CustomerSupport.slnx` — clean, warnings-as-errors.
2. `cd backend && dotnet test CustomerSupport.slnx --filter "FullyQualifiedName~<new test class>"`
   — paste output.
3. `cd backend && dotnet test CustomerSupport.slnx` — full suite green before the slice's final
   commit (slice 2 especially: the priority removal touches many fixtures).
