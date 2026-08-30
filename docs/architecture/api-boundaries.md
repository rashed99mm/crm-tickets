# API boundaries

> **Amended 2026-08-25.** The backend is the adapted CCE Platform
> ([ADR-0009](../adr/0009-adopt-the-support-platform-as-the-crm-baseline.md)): eight projects, two hosts
> (`CustomerSupport.InternalApi`, `CustomerSupport.ExternalApi`) over `CustomerSupport.Api.Shared`.
> Project names below have been updated; where this file describes endpoints or a response envelope
> from the previous implementation, the platform's `Result<T>` contract is the one in force.


The contract surface of the API. The response envelope is **specified and partially tested**; the
endpoint inventory is **planned**, derived strictly from specified stories — no endpoint exists
ahead of a story, and none is invented here.

There are two hosts — [`InternalApi`](system-overview.md) (internal staff) and `ExternalApi`
(customer-facing, independently hosted) — sharing one composition core in
[`Api.Shared`](../adr/0008-two-api-hosts-shared-composition-core.md). Everything on this page
applies to both: one envelope, one pipeline order, one catalogue. A host differs only in which
rate-limit policy guards its endpoints and how its health probe names itself.

## Response envelope (settled)

Every endpoint returns the same shape, success or failure (`FND-1`, [`US-101`](../requirements/user-stories/US-101-uniform-response-envelope.md)):

```json
{
  "success": true,
  "code": "CON001",
  "message": { "ar": "...", "en": "..." },
  "data": { },
  "errors": [],
  "traceId": "00-…",
  "timestamp": "2026-08-24T10:00:00Z"
}
```

- `errors` is always an array, never null; `data` is null on failure.
- Success carries a `CON` code and non-empty message; failure an `ERR`/`VAL` code (`FND-2/3`).
- `traceId` is the W3C trace id from `Activity.Current` — it matches what the server logs
  (`FND-6`, `NFR-10`). Presence is tested; log-match is not yet provable (coverage notes this).
- Codes are stable per condition (`AC-51`), every code resolves to a bilingual message
  (`FND-18..21`), and no distinct condition shares another's code.

## Outcome → status mapping

Mapping lives at the boundary only — the domain throws typed failures and never knows HTTP
(`FND-4/8`, [`US-102`](../requirements/user-stories/US-102-outcome-to-status-mapping.md)). Success is
200 (201 where a resource was created); validation failures map to 400 with field-keyed entries;
not-found to 404; duplicate email conflict to 409 (`AC-9`); authorization refusals to 401/403. The
full table is normative in the foundation spec, §FND-5.

## Validation errors

Field-keyed, one entry per failed field, all fields reported in one response:

```json
{ "field": "email", "code": "VAL002", "message": { "ar": "...", "en": "..." } }
```

`field` is camelCase matching the request DTO property so Angular forms can bind errors directly
(`FND-9..11`). Validation runs through the reflection-free pipeline — explicit validators, registered
per request type (`FND-12..13a`) — so renaming a property breaks a test, not production behaviour.

## Wire conventions

- camelCase properties, ISO 8601 UTC dates (`NFR-16`, `AC-54`) — camelCase asserted by test;
  the date half awaits the first dated DTO.
- Every collection endpoint is paginated with a server-enforced maximum page size (`NFR-2`) —
  no unbounded list ever ships.
- No API versioning yet: greenfield has nothing to migrate; if a breaking change ever becomes
  necessary, versioning starts then as an ADR (the foundation spec's own recommendation).

## Planned endpoint inventory

Derived from specified stories only. All require authentication except where noted.

| Area | Endpoints | Stories |
|---|---|---|
| Auth | sign-in | `US-112`–`US-113` |
| Customers | create · search/list (paginated) · read · update · delete-guard · notes CRUD subset · attachments upload/retrieve/delete | `US-001`, `US-004`, `US-002`, `US-117`, `US-007`, `US-006`, `US-130`, `US-008`, `US-131`–`US-133` |
| Tickets | create · list/filter/sort (paginated) · assigned-to-me · detail · status transitions incl. reopen · assign · history | `US-009`, `US-013`, `US-038`, `US-035`, `US-010`, `US-128`, `US-016`, `US-118`, `US-026`, `US-014`, `US-119`, `US-120`, `US-022` |
| Platform | health + OpenAPI document | `US-111` |

Everything else on the roadmap (SLA, conversations, portal, reporting…) has **no endpoint here**
because it has no spec yet.

## Boundary mechanics

- Minimal-API endpoints stay thin: bind → validate → dispatch → map outcome → envelope. The
  shared composition core (`CustomerSupportApiComposition` in `Api.Shared`) stamps `traceId`
  from the current activity and `timestamp` from the injected clock — nothing else touches them.
  Hosts declare only identity (rate-limit policy, health naming); they cannot re-implement the
  pipeline.
- Authorization is enforced here and in the domain, never only in the UI (see
  [security.md](security.md)).
- Documentation and health come from the same metadata pipeline producing `Response<T>` metadata
  per route (`FND-30..32`); the authenticated-UI-executes-request half of `FND-31` waits for
  `US-112`.
