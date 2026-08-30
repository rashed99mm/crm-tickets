---
name: dotnet-clean-architecture
description: Use when writing, structuring or reviewing any .NET backend code in this project - covers the layered layout (four layers, six projects) and dependency rule, where business logic belongs, request validation, the error contract, and EF Core configuration and migrations
---

# .NET 10 Clean Architecture

## Overview

Four layers, six projects, dependencies pointing inward only. The layout exists to answer one
question consistently: *where does this code go?*

```
backend/src/CustomerSupport.Domain/          entities, value objects, domain events   → depends on NOTHING
backend/src/CustomerSupport.Application/     use cases, handlers, validators, ports   → depends on Domain
backend/src/CustomerSupport.Infrastructure/  EF Core, repositories, external services → depends on Application
backend/src/CustomerSupport.Api.Shared/      shared composition core, envelope middleware, rate limiting,
                                              ICurrentUser, health mapping             → depends on Application + Infrastructure
backend/src/CustomerSupport.InternalApi/     staff host — thin                        → depends on Api.Shared (+ all three)
backend/src/CustomerSupport.ExternalApi/     customer host, independently hosted — thin → same
```

All backend paths above are relative to the repository root's `backend/` folder (mirroring
`frontend/`). The solution file is `backend/CustomerSupport.slnx`.

## The dependency rule

**This is the one invariant that must not bend.** It is also the only architectural claim an
assessor can verify mechanically — open `Domain.csproj`, look for references, done.

- `Domain.csproj`: zero `ProjectReference`, zero persistence packages. No `DbContext`, no EF
  attributes, no `[JsonPropertyName]`, no `IRepository`.
- `Application.csproj`: references `Domain` only. Declares *port interfaces*
  (`IEventRepository`, `IClock`, `IEmailSender`) that `Infrastructure` implements. This
  inversion is the entire point — Application states what it needs, Infrastructure supplies it.
- `Infrastructure.csproj`: references `Application`. Owns `DbContext`, EF configurations,
  HTTP clients.
- `CustomerSupport.Api.Shared.csproj`: references Application + Infrastructure. All transport plumbing
  lives here; hosts stay identity-only ([ADR-0008](../../docs/adr/0008-two-api-hosts-shared-composition-core.md)).
- `CustomerSupport.InternalApi.csproj` / `CustomerSupport.ExternalApi.csproj`: reference `Api.Shared`.
  Composition happens by calling the shared core; a host adds nothing but its endpoints and naming.
  There is no separate `AdminApi`/`CustomerApi` project — that orphaned pre-ADR-0008 project was
  deleted; `InternalApi`/`ExternalApi` are the only two hosts.

**Enforce it in the project files, not by discipline.** Adding a reference must feel like a
decision, not a convenience. When you find yourself wanting `Application → Infrastructure`, the
answer is a port interface, never a reference.

## Where code goes

| Kind of logic | Home | Test |
|---|---|---|
| Invariant that is always true of an entity (an event's end is after its start) | `Domain` — enforce in the constructor or a factory method | Unit, no mocks |
| Use case orchestration (load, mutate, persist, publish) | `Application` handler | Unit, ports faked |
| "Does this name already exist?" | `Application` handler, via a port | Unit + integration |
| SQL, indexes, column types | `Infrastructure` EF configuration | Integration |
| Status codes, routing, auth attributes | host project (`InternalApi`/`ExternalApi`) or shared endpoint mapping in `Api.Shared` | Integration |

**Keep entities from becoming anaemic.** An entity that is only public setters has moved its
rules into handlers, and the same rule then gets re-implemented in each handler that forgets.
Prefer private setters plus intention-named methods (`event.Reschedule(newStart)`).

## Requests, handlers, validation

One request type, one handler, one validator per use case, in a feature folder:

```
Application/Events/CreateEvent/
  CreateEventCommand.cs      request + response DTO
  CreateEventHandler.cs      orchestration
  CreateEventValidator.cs    input shape rules
```

**On MediatR:** MediatR moved to commercial licensing (v13+). For this project either pin the
last Apache-2.0 version or use a small hand-rolled dispatcher — a dozen lines over
`IServiceProvider` covers everything this project needs, and it is easier to defend under
questioning than a library you did not choose deliberately. **Verify the current license terms
before adding MediatR, AutoMapper or FluentValidation** — several of that family changed terms
recently. Whatever you pick, record it as an ADR.

Validation splits in two, and conflating them causes the classic bug where a rule lives in only
one place:

- **Input shape** (required, length, range, format) → validator, runs before the handler,
  returns 400 with per-field errors.
- **Business rules** (uniqueness, state transitions, authorisation over a specific record) →
  the handler, because they need data. Returns 409/404/403 as appropriate — *not* 400.

Run validators in a pipeline behavior or a filter, once, centrally. Per-handler `if (!valid)`
blocks drift the moment one handler is written by someone in a hurry.

## The error contract

One shape for every failure. RFC 9457 `ProblemDetails`, which ASP.NET Core produces natively:

```
400 validation      → ValidationProblemDetails, errors keyed by field
401 / 403           → no body detail beyond the status
404 not found       → ProblemDetails, no hint whether the id ever existed
409 conflict        → ProblemDetails naming the conflicting rule
500 unexpected      → ProblemDetails, generic detail, correlation id, NEVER a stack trace
```

Use `AddProblemDetails()` and one exception-handling middleware. **Never leak internals in a
response** — stack traces, SQL text, connection strings and inner exception chains are a
security finding, not a debugging aid. Log the detail server-side with a correlation id and
return the id.

Expected failures should not travel as exceptions. A "not found" is a normal outcome; prefer a
result type from handlers and reserve exceptions for the genuinely unexpected.

## EF Core

- `IEntityTypeConfiguration<T>` classes, one per entity, in `Infrastructure`. Not
  `OnModelCreating` — it becomes a thousand-line method.
- Explicit column types and lengths. Defaults produce `nvarchar(max)` columns that cannot be
  indexed.
- **Money is `decimal(18,2)`, never `float` or `double`.** Binary floating point cannot
  represent 0.01 and the rounding errors surface in totals.
- Queries that leave a handler are projections (`.Select(...)` to a DTO). Never return entities
  from an endpoint — it couples the API contract to the schema and over-fetches.
- `.AsNoTracking()` on reads.
- **Watch for N+1.** A `.Include()` missing inside a loop is the most common performance defect
  in this shape of code; check the generated SQL when a list endpoint feels slow.
- Migrations are reviewed before being applied. Read the generated `Up`/`Down` — EF will
  cheerfully generate a drop-and-recreate that loses data. Migrations are committed with the
  code that needs them.

## Code hygiene: no magic values, XML docs that are true

Two rules, enforced the same way the rest of this file argues for: in the code, not by review.

### No magic strings or magic numbers

A literal that means something beyond the line it's written on — an error code, a config name, a
SignalR group prefix, a retry count, a status string compared more than once — gets a named
constant, not a repeated inline literal. This project already has the pattern in two places; copy
it rather than inventing a third shape:

- **Error/success codes** → `Application/Errors/ApplicationErrors.cs`, one nested `static class` per
  feature area (`ApplicationErrors.Ticket.MESSAGE_RECORDED = "TICKET_MESSAGE_RECORDED"`), each with a
  matching `ar`/`en` pair in `Api.Shared/Localization/Resources.yaml` — enforced mechanically
  (`EveryErrorCode_HasABilingualMessage` fails the build if a code has no translation, so a
  hardcoded literal that skips the constant also skips that check).
- **Config names, SignalR groups, retry counts** → one small `static class` per concern, e.g.
  `Application/Notifications/NotificationGatewayConstants.cs` (`EmailGatewayConfigName`,
  `SmsGatewayConfigName`, `TransientRetryCount`, `SignalRUserGroupPrefix`).
  `EmailNotificationChannelSender` and `SmsNotificationChannelSender` both reference it instead of
  writing `"EmailGateway"` / `"SmsGateway"` inline — check either sender for a raw string literal
  that carries meaning and there isn't one.
- A string compared against a fixed set more than once — `TicketMessage`'s `Direction`/`Channel`,
  a session's `Status` — gets its `AllowedX` array, or a real value object
  (`NotificationChannel`, `TicketStatus`), defined in exactly one place. Two independent literal
  arrays that are supposed to agree with each other is the exact failure mode this rule exists to
  prevent — it has already happened once in this codebase (`TicketMessage.cs`'s `AllowedChannels`
  and `RecordTicketMessageCommandValidator.cs`'s copy of the same list, caught while grounding the
  `FEAT-24` communication-channels plan — see `sdd-workflow`'s "Tasks are execution plans" section).
- **One-off literals with no meaning beyond their line** are not this rule's target — a `"Normal"`
  passed once as a default priority, a single `MaximumLength(4000)` with no second copy anywhere,
  do not need a name to prove they're consistent with nothing. Constants exist to keep two things in
  sync; a literal that is only ever one thing doesn't have that job.

### XML doc comments, and they have to be true

Every public type and public member gets a `///` doc comment — not boilerplate ("Gets or sets the
name"), a comment that says what the signature alone can't: why a setter is private, why a field can
be null, what invariant a validation enforces, why a business rule holds the way it does. Every
class in this codebase already does this — `Ticket.cs`, `TicketMessage.cs`, `Customer.cs`, every
channel sender — read one before writing a new one, and match its register rather than a generic
template.

**"True" means checked against the method it documents, not assumed to have stayed true.** A
comment describing behaviour the code no longer has is worse than no comment — it actively misleads
the next reader, and the person defending this code in review. When a method's behaviour changes,
its doc comment is part of that diff, not a follow-up task.

**Not mechanically enforced yet — a real gap, not a decision.** `backend/Directory.Build.props:7`
currently suppresses `CS1591` (`NoWarn` includes `1591`) even though `GenerateDocumentationFile` is
`true` and the rest of this project builds with `--warnaserror`. A missing XML comment on a public
member compiles clean today — unlike this project's other mechanically-enforced rules (bilingual
error messages via a test, the dependency rule via `.csproj` references). Removing `1591` from
`NoWarn` would make this rule self-enforcing the same way. That hasn't been done as a side effect of
writing this rule down, because flipping it would surface however many pre-existing gaps already
exist across the whole solution at once under `--warnaserror` — a deliberate call for whoever does
it, with its own PR, not a drive-by change.

## Red flags

| Thought | Reality |
|---|---|
| "Just one reference from Application to Infrastructure" | That is the architecture gone. Use a port interface. |
| "I'll put this rule in the handler for now" | If it is always true of the entity, it belongs in the entity, or it gets duplicated. |
| "Returning the entity directly is simpler" | It couples your API contract to your DB schema and leaks columns you did not mean to expose. |
| "I'll return the exception message so debugging is easier" | That is an information-disclosure finding. Correlation id instead. |
| "400 covers all validation" | Uniqueness is 409, missing is 404, forbidden is 403. Status codes are part of the contract. |
| "The migration looks fine, EF generated it" | EF generates data-destroying migrations without warning. Read the Down method. |
| "It's just one config name, a raw string is fine here" | It's fine until a second sender needs the same string and someone retypes it slightly differently. One constant, referenced everywhere. |
| "The XML comment restates the method name" | That's decoration, not documentation. Say what the signature can't. |
| "I'll update the doc comment later" | Later is when it becomes false and nobody notices. It's part of the same diff as the behaviour change. |
