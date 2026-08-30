---
name: api-contract
description: Use when designing or changing an API endpoint, DTO, pagination or error shape, or when wiring the Angular frontend to the backend - keeps the two stacks from drifting via OpenAPI and a generated typed client
---

# API contract

## Overview

The backend and frontend are separate builds that can disagree. A renamed field compiles on both
sides and fails at runtime, usually during a demo. The contract is what turns that into a build
error instead.

**OpenAPI is the single source of truth: generated from the backend, consumed by the frontend as
a generated TypeScript client.** Hand-written frontend interfaces mirroring backend DTOs are two
copies of one truth, and they diverge silently.

## Producing the document

.NET 10 generates OpenAPI natively through `Microsoft.AspNetCore.OpenApi` — `AddOpenApi()` plus
`MapOpenApi()`. Swashbuckle is not needed; it was dropped from the templates in .NET 9. .NET 10
emits OpenAPI 3.1 by default, so confirm the client generator you pick supports 3.1 — some still
assume 3.0 and fail confusingly on a 3.1 document. Verify the exact packages and the UI choice at
scaffolding time rather than trusting this note.

Annotate endpoints so the document is actually usable: declare every response type and status
(`.Produces<EventDto>(200).ProducesProblem(404)`). An endpoint documented as returning only 200
generates a client that cannot represent its own failure cases.

## Consuming it

Generate the TypeScript client into a folder clearly marked generated, never hand-edited, and
regenerate as a build step. When the backend renames a field, the frontend build breaks — that is
the entire purpose.

If generation is not wired up yet, the fallback is one hand-written model file per feature, kept
next to its service and reviewed against the OpenAPI document. Treat that as temporary and record
it, because it *will* drift.

## Conventions

Consistency matters more than which convention wins — the frontend pays for every exception.

- **Naming:** `PascalCase` in C#, serialised as `camelCase` so TypeScript reads naturally. Set it
  once in JSON options; never per-DTO.
- **Dates:** ISO 8601 UTC on the wire, always. Send `2026-08-23T14:30:00Z`, never a local time,
  and never a bare date for something that is a moment. Timezone bugs from this are painful to
  find and embarrassing to demo.
- **Money:** a decimal string or minor units, with the currency alongside. Never a float.
- **Ids:** one style throughout, and never expose a sequential integer where enumeration matters.
- **Nulls:** omit or send null, consistently. Do not let "absent" and "null" mean different
  things unless that distinction is specified.

### Collections

Every list endpoint is paginated from the first commit. Retrofitting pagination changes the
response shape, which breaks every consumer.

```json
{ "items": [], "page": 1, "pageSize": 20, "totalCount": 137 }
```

Cap `pageSize` server-side. An uncapped page size is a denial-of-service vector — a caller asking
for a million rows should get a 400, not a timeout.

### Errors

RFC 9457 `ProblemDetails` for every failure, with validation errors keyed by the field name from
the request DTO so the frontend can attach them directly to form controls:

```json
{ "title": "Validation failed", "status": 400,
  "errors": { "title": ["Must be 200 characters or fewer"] } }
```

**One error shape for the whole API.** A frontend error interceptor can handle one shape
reliably; two shapes mean every call site guesses.

## Changing the contract

- Additive changes (a new optional field) are safe.
- Removing or renaming a field, or narrowing a type, breaks consumers: change both sides in one
  commit and retest the affected acceptance criteria.
- Never change what a field *means* while keeping its name. That breaks consumers silently, and
  silent breakage gets found in production.

## Red flags

| Thought | Reality |
|---|---|
| "I'll write the TS interface by hand, it's quicker" | Two copies of one truth. They drift, and the drift surfaces at runtime. |
| "I'll add pagination when the list grows" | The shape change breaks every consumer. Paginate from commit one. |
| "This endpoint can return a different error shape" | Then every frontend call site needs bespoke handling. One shape. |
| "The date works locally" | Local time on the wire is a timezone bug waiting for a different machine. UTC, ISO 8601. |
| "Returning the entity is fine" | It publishes columns you did not mean to expose and couples the contract to the schema. |
