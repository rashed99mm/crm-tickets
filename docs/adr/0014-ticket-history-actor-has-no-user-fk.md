# ADR 0014 — Drop the `TicketHistory.ActorId` FK: a system actor is not a user

- **Status:** Accepted
- **Date:** 2026-08-27

## Context

US-218 drives multi-level automatic escalation. Each escalation advance runs through
`Ticket.AdvanceEscalation(fromLevel, toLevel, systemActor)`, which appends exactly one `Escalated`
history row recording which level the ticket moved *from* and *to*, and stamps that row with a fixed,
well-known system actor identity (`SystemActors.EscalationEngine`).

The spec addendum A10 is explicit that this is **"a system action, not a session action"** — there is
no logged-in user performing a breach-time escalation. The escalation engine decides, and the history
record has to bear witness to *which* engine actor raised the level, not to a person.

The adopted platform baseline ([ADR-0009](0009-adopt-the-cce-platform-as-the-crm-baseline.md)) gave
`TicketHistory.ActorId` a hard foreign key to `AspNetUsers.Id` with `Restrict` delete behaviour. That
conflicts with the fixed, non-user system actor by construction: the actor GUID can never name a row in
`AspNetUsers`, so every `Escalated` history write failed as a `DbUpdateException` surfacing as an SQL
foreign-key violation. The engine could not record its own action.

The codebase already handles other audit stamps this way. `CreatedBy`, `UpdatedBy` and the `SLAEvent`
actor attributes are carried as plain identity columns with **no** user FK — they are audit attributes,
not referential relationships. `ActorId` was the odd column out.

## Decision

Drop the `TicketHistory` foreign key to `AspNetUsers` (migration `20260827104722_DropTicketHistoryActorFk`),
removing both `FK_TicketHistory_AspNetUsers_ActorId` and its supporting index `IX_TicketHistory_ActorId`.

`ActorId` becomes an audit attribute on the history row, exactly like `CreatedBy`/`UpdatedBy`/`SLAEvent`
elsewhere: it records *who or what* produced the change and is not a DB-enforced link to a user row.
Record-level integrity is preserved by the real relationship the row depends on — the `TicketId` FK,
whose foreign key is retained. Real, session-backed actions still store the acting user's id; they just
do so without the database asserting the reference.

The mapping layer documents the decision where the schema used to imply it — the comment in
`TicketHistoryConfiguration` records the reasoning inline for the next developer who reads the
configuration.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **Keep the FK and seed the system actor as a real `AspNetUser` row** | Pollutes the user directory with a non-person, contradicts the fixed well-known GUID contract that lets every layer refer to the engine actor by a constant, and exposes a fake user to authentication, authorization and user listing. The identity is a *system* identity, not a soft user who happens never to log in. |
| **Nullable FK: `NULL` for system actions, the user FK for real ones** | The escalation actor is a real, repeated, auditable event identity — not an absent one. `NULL` erases *which* engine actor advanced the ticket and forces every history read to branch on an optional join. It also still cannot admit the well-known actor GUID, so the meaningful case stays unsupported. |
| **Keep the user FK and add a separate `SystemActorId`/audit column for system writes** | Two actor-shaped columns carrying the same concept, one almost always empty, more schema surface and more mapping — with no integrity win, because the user FK still cannot witness a console or system write. |
| **A second small table of system actors, verified by an FK or app-side lookup** | Over-engineering for the current need: `ActorId` is an audit stamp, not a queried relation, and no feature this pass looks a history row up *by* its actor. Worth revisiting if actor-scoped history queries ever need database integrity. |

## Consequences

**Easier.** Escalation history writes succeed, and the actor column now behaves uniformly with every
other audit stamp in this codebase instead of being the one column with a special, incompatible
constraint.

**Harder.** The schema no longer guarantees that an `ActorId` names a user; a mistyped actor identity is
no longer caught by the database. That risk is bounded here by the fixed system-actor constant (one
code path supplies it) and by the fact that real user actions continue to store the real user id by
convention. The audit trail's *integrity* still rests on the `TicketId` FK and the append-only guard
([ADR-0010](0010-append-only-history-enforced-by-a-savechanges-guard.md)), neither of which is weakened.

**Hard to reverse (mildly).** Re-adding the FK to `AspNetUsers` is a trivial forward migration, but
rows already holding system-actor GUIDs would fail it unless the link were made conditional or the
system identity migrated to a real user row. The re-add should therefore never be done blindly — it is
not a drop-and-recreate, it is a data-settlement problem.
