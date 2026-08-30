# Domain model

Initial domain model for slice S1. The authoritative persistence shape is the S1 schema spec —
[`../superpowers/specs/EPIC-12-US-000-s1-schema.md`](../superpowers/specs/EPIC-12-US-000-s1-schema.md);
this view explains the *why* behind those tables and the invariants the domain enforces.
Nothing here is implemented yet except the base types (`US-108`, `FND-22/27/28`).

## Bounded context

One context: support operations. Customers, tickets and their history are one cohesive model —
deliberately not split into micro-contexts. The roadmap's later concerns (SLA, knowledge base,
portal) will get their own modelling passes when specified; this document grows per slice rather
than speculating ahead.

## Aggregates

| Aggregate | Root | Tables | Notes |
|---|---|---|---|
| Customer | `Customer` | `customers`, `customer_notes`, `customer_attachments` | Profile plus its notes and attachment links. Delete is a guard (`US-117`), never a removal |
| Ticket | `Ticket` | `tickets`, `ticket_history` | Lifecycle moves are the only mutations of status; every move appends an event row (`US-121`) |

`Asset` sits beside the aggregates rather than inside one: it is the shared file catalogue every
surface links into (2026-08-25 schema revision), so it is owned by the file-store boundary, not
by Customer. A future Ticket↔attachment link will reference the same catalogue.

Staff users (`US-112`–`US-115`) are **not** modelled as a domain aggregate — identity is an
application/infrastructure concern; the domain sees only "which actor performed this" (`BR-6`:
recorded from the authenticated session, never from payload, ignored if supplied).

## Entities and key fields

Derived from the schema spec; column-level detail lives there. Rendered relationships:
[erd.md](erd.md).

```text
Customer            id · full_name · email(unique) · phone · company · created_at/by · updated_at/by · is_deleted
CustomerNote        id · customer_id → customers · body · author_id · created_at/by          (immutable)
Asset               id · original_name · stored_name(unique, server-generated) · content_type · size_bytes · uploaded_by · created_at/by · is_deleted
CustomerAttachment  id · customer_id → customers · asset_id → assets (unique live link) · created_at/by · is_deleted
Ticket              id · customer_id → customers · title · description · status · priority · assignee_id? · created_at/by · updated_at/by · row_version
TicketHistory       id · ticket_id → tickets · actor_id · field_changed · old_value? · new_value? · changed_at  (append-only)
```

## Value objects and base types

Settled by `US-108`: `Email` with format validation, `Money`-style guarded primitives where they
earn their keep, entity/guid base types, audit-column plumbing, soft-delete flag on a shared base.
Domain types throw typed failures; mapping to HTTP status codes happens at the boundary
(`US-102`, `FND-4/8`) — the domain never knows about HTTP.

## Invariants

| Invariant | Source | Enforced by |
|---|---|---|
| Email unique among live customers | `AC-9`, `FR-2.2` | DB unique index + conflict mapping (`US-116`) |
| Delete refused when open tickets exist | `AC-15..16`, `BR-7` | Domain guard (`US-117`) |
| Only defined lifecycle transitions occur | `AC-38..39`, `BR-4` | State machine in `Ticket` (`US-118`) |
| Reopen follows the ordinary ownership rule — agent if assigned to them, supervisor for any — plus the concurrency guard | `AC-40..41`, `BR-11`, `BR-13` | Ownership check + `row_version` compare (`US-026`, `US-120`) |
| Status change belongs to assignee | `AC-45..47`, `BR-11` | Authorization rule (`US-120`) |
| History rows immutable | `AC-48..49` | No update path exists; append-only repository (`US-121`) |
| Note author = authenticated actor | `AC-19`, `BR-6` | Taken from session, payload value ignored (`US-007`) |
| Attachment filename cannot escape storage | `AC-25` | Stored/generated name decoupled from client-supplied name (`US-131`) |
| One live ownership link per asset | 2026-08-25 schema revision | Unique filtered index on `AssetId` (`US-008`) |
| Every response bilingual-capable | `BR-22` | Message catalogue keyed by code (`US-106`) |

**Corrected 2026-08-25.** Three citation errors above are fixed as of this revision. The delete
guard was miscited as `BR-1` ("a ticket belongs to exactly one customer"); the actual guard is
`BR-7`. The reopen row named "Team Lead or Manager" — roles that exist nowhere else in this
documentation set — and cited `BR-5`/`BR-7` (append-only / delete guard), neither of which
restricts reopen; no BRD rule or acceptance criterion restricts reopen to a role, and `AC-40`
describes it as an ordinary transition. Reopen is governed by the same ownership check as any
other status change (`BR-11`), plus the concurrency guard (`BR-13`). The status-change row itself
was also miscited as `BR-7`; the correct rule is `BR-11`. See
[erd.md §3](erd.md#3-s1-core--built) for the matching correction to the assignment-role citation.

## What is deliberately not modelled yet

SLA deadlines, conversation messages (`G-3`), branches/tenants (`OQ-6`), knowledge articles,
notifications. Each waits for its slice's spec; adding tables ahead of specification is how phantom
requirements get born.
