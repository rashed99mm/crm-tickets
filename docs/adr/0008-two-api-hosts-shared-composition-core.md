# ADR 0008 — Host Admin and Customer APIs separately behind one composition core

- **Status:** Accepted
- **Date:** 2026-08-25

## Context

ADR-0002 fixed the logical layering (Domain → Application → Infrastructure, one API host on top).
Since then a deployment requirement became explicit: the internal staff surface and the
customer-facing surface will be **hosted independently**. They will deploy, scale, expose and
harden on different schedules — an internal tool that can assume staff identity is not operated
the same way as a public endpoint, and neither should an outage or a release of one force the
other.

Two hosts therefore exist: `CustomerSupport.InternalApi` (internal) and
`CustomerSupport.ExternalApi` (customer-facing). Both must return the same envelope, run the same
pipeline in the same order, serialize identically, and share the message catalogue — the product
is one; its deployment boundary is two.

The first cut of this split copied ~40 lines of composition plumbing into each `Program.cs`,
differing only in two strings. That is the failure mode this decision exists to prevent: two
hosts drifting until "identical product surface" quietly becomes false.

## Decision

Two independently hosted API projects over one shared composition core, preserving ADR-0002 four
*layers* across more than four csproj files.

**Amended 2026-08-25 by ADR-0009.** The project set is now eight, because the support platform reference
was adopted as the baseline. The decision itself is unchanged and the adopted platform happened to
want exactly this shape, which is why it survived the pivot intact:

| Project | Role |
|---|---|
| `CustomerSupport.Domain` | entities, value objects, events, specifications |
| `CustomerSupport.Application` | CQRS features via MediatR, contracts, behaviors |
| `CustomerSupport.Infrastructure` | EF Core, Identity, messaging, jobs, localization |
| `CustomerSupport.Shared.Contracts` | message contracts shared with external consumers |
| `CustomerSupport.Api.Shared` | the composition core both hosts share |
| `CustomerSupport.InternalApi` | staff host: full surface, seeds on start |
| `CustomerSupport.ExternalApi` | customer host: narrow, read-only, anonymous, no seeding |
| `CustomerSupport.Migrator` | schema tool |

`Api.Shared` owns everything transport-shaped that both hosts share: the exception middleware,
localisation middleware, SignalR hubs, rate limiting, authentication and authorisation wiring,
OpenAPI configuration including the XML-documentation transformer, and the composition core itself
(`AddPlatform*` / `UsePlatform*`). Each host `Program.cs` declares only what makes it that host.

The extraction was not free and is worth recording: the reference kept all of this inside its single
API project, so moving it into a library required declaring the Web SDK implicit usings explicitly
rather than adopting the Web SDK for code that is not an application.

**What the split actually buys, concretely.** `ExternalApi` does not merely authorise the staff
surface away — it does not contain it. There is no user-management controller to reach, no platform
settings, no integration configuration, and it does not seed. A customer-facing deployment cannot
leak an endpoint it was never compiled with, and that is a stronger guarantee than any policy.

Dependency direction is unchanged and still points inward: hosts reference `Api.Shared`; `Api.Shared`
references `Application` and `Infrastructure`; nothing inward references outward. The dependency rule
was never "exactly four csproj files" — it is the direction of the arrows, and it still holds.
`Domain` has zero project references, verified.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **Single host** (as ADR-0002 assumed) | Contradicts the hosting requirement: no independent deploy/scale/hardening of the public surface, and every future customer-facing hardening concern would entangle with staff internals. |
| **Two fully independent solutions** | Duplicates domain/application/infrastructure code to preserve independence nobody asked for. The product has one database schema and one set of business rules; only the deployment edge differs. |
| **Host B referencing host A for shared plumbing** | Makes one deployable depend on another's binary; coupling at exactly the layer where independence is required. |
| **Shared plumbing in Application/Infrastructure** | Puts middleware, HttpContext access and rate limiting inside layers that must stay framework-free — precisely what ruling R15 forbids. |

## Consequences

- Easier: independent hosting, per-surface rate limits (`api-admin` / `api-customer` / `api-common`
  policies already distinct), and a host file short enough that drift between hosts is visible at
  a glance.
- Easier: adding a third surface later (portal API, reporting API) is a new thin host plus
  existing core, not a refactor.
- Harder: the shared configuration story needs care — each host ships its own `appsettings.json`
  and gets `Resources.yml` copied into its output at build time. There is deliberately **no**
  shared runtime config file; changes must be made once in source and reach both hosts through
  their own builds.
- Cost: both hosts currently point at the same database via identical connection strings in
  separate config files. Splitting persistence per host is out of scope and would be a new,
  separately-argued decision.
- Debt stated plainly: rate limiting landed ahead of any story or AC that specifies it. It is
  infrastructure for the hosting requirement above, not a specified feature; when S1 stories grow
  endpoint-level criteria they should cite concrete limits.
