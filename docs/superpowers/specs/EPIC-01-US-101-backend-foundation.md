# Backend foundation — DDD base types, response envelope, localization, CQRS

> **Superseded 2026-08-25 by the platform baseline.** The backend this document describes was
> replaced when the CCE Platform reference was adopted as the CRM baseline — see
> [`EPIC-12-US-000-crm-platform-baseline-design.md`](../specs/EPIC-12-US-000-crm-platform-baseline-design.md).
> The code named below no longer exists in `src/`; it is archived, not deleted. This file is kept
> because it is the record of what was built and why, and deleting it would erase the reasoning
> behind decisions that still hold — the envelope, the localisation approach and the dependency rule
> among them. **Do not follow its steps.**


**Date:** 2026-08-24
**Relates to:** `EPIC-02-US-016-ticket-lifecycle.md` (S1). This is step 1 of that spec's build
order, expanded because the cross-cutting contract turned out to be substantial enough to specify
on its own.
**Criterion ids:** this spec uses the **`FND-n`** prefix. S1 owns `AC-n`. Separate prefixes so the
two documents never collide.

## Problem

S1 needs a project skeleton before any feature can be built. Three cross-cutting concerns have to
be settled first, because retrofitting any of them touches every handler and every endpoint:

1. **Entity foundations** — identity, auditing and soft deletion, currently duplicated per entity
   by default.
2. **The response contract** — what every endpoint returns, on success and on failure. Left
   unspecified, each endpoint invents its own shape and the frontend branches per call site.
3. **System messages** — where user-facing text lives, in two languages, without hardcoding it in
   handlers.

An error-code system alone is not enough. The pattern this project adopts (from the CustomerSupport platform's
refactor plan) makes a specific correction: **success responses need a code and a message too**,
otherwise the frontend hardcodes its own toast text and the backend's message catalogue is only
half the story.

## Assumptions

- **F1.** Every endpoint returns the same envelope, success or failure. The only exception is a
  transport-level rejection that never reaches the pipeline (a 401 from the JWT middleware).
- **F2.** Both languages ship in every response as `message: { ar, en }`. The client selects;
  the server does no content negotiation.
- **F3.** Arabic strings in `Resources.yml` for S1 are placeholders pending review, marked as
  such. The mechanism is real; the translation is S8's work.
- **F4.** `traceId` comes from `Activity.Current?.Id` (a W3C traceparent), not a custom scheme, so
  it correlates with logs and any future OpenTelemetry export.
- **F5.** Entity ids are `Guid` from `Guid.CreateVersion7()` — non-enumerable, and time-ordered so
  clustered index inserts do not fragment the way v4 does.
- **F6.** One message catalogue file for the whole application. Splitting per feature is a later
  concern and would need a merge step.
- **F7.** Codes are permanent once used. A code's meaning never changes; a superseded code is
  retired, not reassigned, because clients switch on it.

## Out of scope

Everything in the source plan that only applies to migrating an existing codebase: handler
migration across ~40 files, deleting deprecated types, frontend breaking-change coordination, and
the `X-Response-Version` backward-compatibility header. Greenfield has nothing to migrate.

Also out: a third language, per-feature message files, hot-reloading the catalogue, and
server-side content negotiation.

## Acceptance criteria

Priority marks the cut order: **P0** must ship, **P1** should, **P2** first to go.

### Response envelope

- **FND-1** (P0) Every endpoint returns `{ success, code, message: { ar, en }, data, errors[],
  traceId, timestamp }`. `errors` is always an array, never null. `data` is null on failure.
- **FND-2** (P0) A success response carries `success: true`, a `CON` code, and a non-empty message
  in both languages.
- **FND-3** (P0) A failure response carries `success: false`, an `ERR` or `VAL` code, a non-empty
  message in both languages, and `data: null`.
- **FND-4** (P0) `MessageType` maps to HTTP status exactly once, in one place:

  | `MessageType` | Status |
  |---|---|
  | `Success` | 200, or 201 where a resource is created |
  | `Validation` | 400 |
  | `Unauthorized` | 401 |
  | `Forbidden` | 403 |
  | `NotFound` | 404 |
  | `Conflict` | 409 |
  | `PayloadTooLarge` | 413 |
  | `UnsupportedMediaType` | 415 |
  | `BusinessRule` | 422 |
  | `Internal` | 500 |

  `PayloadTooLarge` and `UnsupportedMediaType` are additions to the source pattern's enum,
  required by S1's AC-23 and AC-24.
- **FND-5** (P0) Operations that would conventionally return 204 return **200 with the envelope**,
  so the frontend always has a code and message. No endpoint returns an empty body on success.
- **FND-6** (P0) `traceId` is present on every response and matches the `Activity` id written to
  the server log for that request.
- **FND-7** (P1) `timestamp` is set once, at the API boundary, from `IClock` — not by a record
  initializer, so it is deterministic in tests.
- **FND-8** (P0) No response body contains a stack trace, SQL text, or connection string. An
  unhandled exception yields `ERR900` with a generic message and the real detail in the log only.

### Validation errors

- **FND-9** (P0) A validation failure returns 400, top-level code `VAL001`, and one `errors[]`
  entry per failed field: `{ field, code, message: { ar, en } }`.
- **FND-10** (P0) `field` is camelCase, matching the request DTO property so the Angular form can
  bind the error to its control (S1 AC-60).
- **FND-11** (P0) Multiple failures across multiple fields all appear in one response. The
  envelope is not limited to a single error — that limitation is the reason the source plan
  replaced `Result<T>`.
- **FND-12** (P0) Validators carry the message key via FluentValidation's `WithErrorCode()`, not
  by overloading `ErrorMessage`. `ErrorMessage` stays available as a human-readable fallback.
- **FND-13** (P0) The validation pipeline behavior uses **no runtime reflection**. It is declared
  with matching arity and a static-abstract constraint:

  ```csharp
  public interface IFailableResponse<TSelf>
  {
      static abstract TSelf ValidationFailure(
          string code, LocalizedMessage message, IReadOnlyList<FieldError> errors);
  }

  public sealed class ResponseValidationBehavior<TRequest, TResponse>
      : IPipelineBehavior<TRequest, TResponse>
      where TRequest : notnull
      where TResponse : IFailableResponse<TResponse>
  ```

  `TResponse.ValidationFailure(...)` is then a compile-time-checked call.

  **Corrected 2026-08-24 after empirical testing.** This criterion originally specified
  `IPipelineBehavior<TRequest, Response<TData>>` registered as an open generic. That does not
  work: `Microsoft.Extensions.DependencyInjection` will not unify the nested generic
  `Response<TData>` against a closed `Response<CustomerDto>`, so the behavior is **silently
  skipped** — no exception, no log, the handler simply runs unvalidated. Verified against MediatR
  12.5.0 on .NET 10 with both `AddTransient(typeof(IPipelineBehavior<,>), …)` and MediatR's own
  `AddOpenBehavior`; the behavior never executed in either case. This is very likely why the
  source plan reached for reflection.

- **FND-13a** (P0) A test asserts the behavior **actually executes**: given a request that fails
  validation, the handler must not run and the response must carry `VAL001`. Because the failure
  mode above is silent, a pipeline that is wired wrongly passes every test that does not check
  this specifically. This test is the guard against a no-op validation layer.

### Message codes and localization

- **FND-14** (P0) Codes are prefixed `ERR` (failures), `CON` (confirmations) and `VAL`
  (field-level validation), each numbered uniquely. No two distinct messages share a code.
- **FND-15** (P0) `Resources.yml` is one flat file keyed by domain key, each with `ar` and `en`:

  ```yaml
  REQUIRED_FIELD:
    ar: "هذا الحقل مطلوب"
    en: "This field is required"
  ```
- **FND-16** (P0) The catalogue is parsed **once at startup** into an immutable dictionary, not
  per request.
- **FND-17** (P0) Malformed YAML, or a duplicate key, **fails at startup** with a message naming
  the file and key — not on the first request that needs that string.
- **FND-18** (P0) A guard test asserts every code constant has a `Resources.yml` entry with
  non-empty `ar` and `en`, and that every mapped domain key resolves to a code. A code cannot ship
  with a blank or missing message.
- **FND-19** (P0) An unmapped domain key is caught by FND-18 at build time. It does not silently
  degrade to `ERR900` at runtime.
- **FND-20** (P1) Placeholder substitution uses named tokens (`{field}`, `{max}`). A missing
  argument leaves the token visible rather than throwing — a localizer that throws while
  formatting an error turns a 409 into a 500.
- **FND-21** (P0) Account lockout returns **the same code and message as invalid credentials**.
  A distinct code would confirm the account exists, defeating S1 AC-2 and AC-6. The real reason is
  logged server-side only.

### Domain base types

- **FND-22** (P0) `BaseEntity<TId>` provides `Id` with a protected setter and identity-based
  equality. Two instances with the same id and type are equal.
- **FND-23** (P0) `IAuditable` (`CreatedAtUtc`, `CreatedBy`, `ModifiedAtUtc?`, `ModifiedBy?`) is
  populated by a `SaveChanges` interceptor from `ICurrentUser` and `IClock` — never assigned by a
  handler.
- **FND-24** (P0) `ISoftDeletable` (`IsDeleted`, `DeletedAtUtc?`, `DeletedBy?`): a delete on such
  an entity is rewritten by the interceptor into an update. No row is physically removed.
- **FND-25** (P0) A global query filter excludes soft-deleted rows from every query, applied by
  reflection over entity types in `OnModelCreating` — one place, not per entity.
- **FND-26** (P0) Unique indexes on soft-deletable entities are **filtered**
  (`WHERE IsDeleted = 0`), so a deleted customer's email address can be reused. Without this,
  S1 AC-9 and AC-16 contradict each other.
- **FND-27** (P1) `ValueObject` provides component-based equality. `Email` and `TicketReference`
  derive from it and validate on construction — an invalid instance cannot exist.
- **FND-28** (P1) `IAggregateRoot` marks `Customer` and `Ticket`. `CustomerNote`,
  `CustomerAttachment` and `TicketHistory` are not roots and are reached through theirs.

### Composition

- **FND-29** (P0) `Domain` has zero project references and no persistence, Identity, or
  serialisation packages. Verified by inspecting `Domain.csproj`.
- **FND-30** (P0) The OpenAPI document describes the envelope truthfully: endpoints declare
  `Produces<Response<T>>` for their success and failure statuses.
- **FND-31** (P1) Scalar UI serves the document and can execute an authenticated request.
- **FND-32** (P0) A health endpoint returns the standard envelope, proving the whole pipeline
  composes before any feature exists.

## Design

### Types and where they live

| Type | Project | Purpose |
|---|---|---|
| `BaseEntity<TId>`, `IAggregateRoot`, `IAuditable`, `ISoftDeletable`, `ValueObject` | `Domain/Common` | Entity foundations |
| `MessageType` | `Domain/Common` | The status-bearing enum |
| `LocalizedMessage(Ar, En)`, `FieldError(Field, Code, Message)` | `Domain/Common` | Envelope pieces |
| `Response<T>` | `Application/Common` | The envelope |
| `SystemCode`, `SystemCodeMap`, `MessageFactory` | `Application/Messages` | Code catalogue and builders |
| `IMessageCatalog`, `IClock`, `ICurrentUser`, `IFileStore` | `Application/Common/Abstractions` | Ports |
| `ResponseValidationBehavior<,>` | `Application/Common/Behaviors` | Validation pipeline |
| `YamlMessageCatalog`, `AuditableInterceptor`, `AppDbContext` | `Infrastructure` | Implementations |
| `ResponseExtensions.ToHttpResult()`, exception middleware | `Api/Common` | Boundary mapping |

`Response<T>` carries `MessageType` as a `[JsonIgnore]` property: it selects the HTTP status and
must not appear on the wire, where `success` and `code` already say everything a client needs.

Only the non-generic `Response` static class exposes the void-command factory. A static
`Response<T>.Ok(code, message)` returning `Response<VoidData>` — as in the source plan — silently
discards `T` at the call site, so it is not carried over.

### Flow

```
Handler ── MessageFactory.NotFound<T>("CUSTOMER_NOT_FOUND")
             ├─ SystemCodeMap: domain key → "ERR010"
             └─ IMessageCatalog: domain key → { ar, en }
           ↓  Response<T> { success:false, code:"ERR010", message, Type=NotFound }
ValidationBehavior (runs first; short-circuits with VAL001 + errors[])
           ↓
Endpoint ── response.ToHttpResult()
             ├─ stamps traceId from Activity.Current, timestamp from IClock
             └─ MessageType → 404
```

Handlers name **domain keys**, never numeric codes. `USER_NOT_FOUND` is readable at the call site
and greppable; `ERR001` is neither. The map is the only place the two meet, which is also what
makes FND-18's guard test possible.

### Code catalogue for this domain

Roughly 40 codes, scoped to S1 — not the CustomerSupport catalogue, which covers news, topics, countries and
knowledge maps that do not exist here.

| Range | Area |
|---|---|
| `ERR001`–`ERR005` | Auth: user not found, invalid credentials, not authenticated, forbidden |
| `ERR010`–`ERR012` | Customers: not found, email exists, has tickets |
| `ERR020`–`ERR024` | Tickets: not found, invalid transition, already in status, not your ticket, concurrency conflict |
| `VAL009`–`VAL010` | Lookups referenced in a request body: category not found, assignee invalid. **`VAL`, not `ERR`** — a body-referenced record that does not exist is a field-level validation failure (400), not a 404, so the prefix must follow `MessageType.Validation`. Corrected 2026-08-24, ruling R14. |
| `ERR040`, `ERR050`–`ERR052` | Notes and attachments: not found, too large, type not allowed |
| `ERR900` | Internal |
| `CON001`–`CON002` | Login, logout |
| `CON010`–`CON012` | Customer created, updated, deleted |
| `CON020`–`CON022` | Ticket created, status changed, assigned |
| `CON030`, `CON040`–`CON041` | Note created, attachment uploaded, deleted |
| `VAL001`–`VAL008` | Validation header, required, email, max/min length, format, enum, range |

There is deliberately **no lockout code** (FND-21).

### Persistence

One `SaveChangesInterceptor` handles both auditing and soft deletion: on `Added` it stamps
created fields; on `Modified` the modified fields; on `Deleted` for an `ISoftDeletable` it flips
the state back to `Modified` and sets the deletion fields. Handlers call `Remove` normally and
soft deletion is transparent — which is the point, and also why the filtered unique index (FND-26)
matters: the row survives and keeps holding its email.

### Dependencies

Every version below was checked against nuget.org on 2026-08-24, including licence metadata —
two packages in this space changed terms recently and the current version of each is not the one
to take.

| Package | Version | Licence |
|---|---|---|
| MediatR | **12.5.0** | Apache-2.0. 13.0.0+ is commercial (see ADR 0005) |
| FluentValidation | 12.1.1 | Apache-2.0 |
| YamlDotNet | 18.1.0 | MIT |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.11 | MIT |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.11 | MIT |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.11 | MIT |
| Microsoft.AspNetCore.OpenApi | 10.0.11 | MIT |
| Scalar.AspNetCore | 2.17.1 | MIT |
| FluentAssertions | **7.2.0** | Apache-2.0. 8.x is commercial (Xceed) |

Pin these exactly. Two are deliberately not the latest version, and a future `dotnet outdated`
run will suggest upgrading both into a licence change.

### Testing

| Level | Covers |
|---|---|
| `Domain.Tests` | `BaseEntity` equality, `ValueObject` equality, `Email`/`TicketReference` validation |
| `Application.Tests` | `MessageFactory` produces the right code and type per domain key; validation behavior builds `errors[]` with camelCase fields and per-field codes |
| Guard test | FND-18: reflection over `SystemCode`, asserting a YAML entry with non-empty `ar` and `en` for every constant, and no orphan keys |
| `Api.IntegrationTests` | Envelope shape on success and failure; every `MessageType` → status mapping; `traceId` present and matching the log; soft delete hidden from queries; filtered index allows email reuse |

The guard test is the highest-value test here: it makes a missing translation a build failure
instead of a blank message in front of a user.

## Build order

1. Solution, four projects, `Directory.Build.props` wired, `Domain` reference-free — **FND-29**
2. Domain base types and value objects — **FND-22** to **FND-28**
3. `Response<T>`, `MessageType`, `LocalizedMessage`, `FieldError` — **FND-1** to **FND-5**
4. `SystemCode`, `SystemCodeMap`, `Resources.yml`, `YamlMessageCatalog`, guard test — **FND-14** to **FND-21**
5. `MessageFactory`, MediatR wiring, `ResponseValidationBehavior` — **FND-9** to **FND-13**
6. `ToHttpResult`, exception middleware, correlation — **FND-6** to **FND-8**
7. `AppDbContext`, interceptor, global filter, first migration — **FND-23** to **FND-26**
8. OpenAPI + Scalar + health endpoint — **FND-30** to **FND-32**

Steps 1–6 are the contract. Nothing in S1 can be built correctly before them, so none of this is
cuttable — which is precisely why it is specified separately and why the S1 feature cut lines
matter more now.

## Amendments to the S1 spec

The envelope replaces `ProblemDetails`, so S1 changes:

- **AC-51** — failures are the envelope with `code` and `message: { ar, en }`, not `ProblemDetails`
- **AC-54** — `camelCase` and ISO 8601 UTC still hold; the shape is the envelope
- **AC-60** — the frontend maps `errors[].field` onto controls, not `ProblemDetails.errors` keys
- **AC-10** — paged results are `Response<PagedResult<T>>`; `items/page/pageSize/totalCount` sit
  under `data`
- **AC-53** — the correlation id is `traceId`, present on all responses rather than only 500s
- **AC-38, AC-39, AC-9, AC-15** — still 409, now carrying `ERR021`, `ERR022`, `ERR011`, `ERR012`
- **AC-23, AC-24** — 413 and 415, requiring the two new `MessageType` members

## Decisions recorded as ADRs

- ADR 0004 — the response envelope over RFC 9457 `ProblemDetails` (taken against recommendation)
- ADR 0005 — MediatR pinned at 12.5.0 for its licence
- ADR 0006 — soft delete by default, with filtered unique indexes
- ADR 0007 — YAML message catalogue with both languages in every response
