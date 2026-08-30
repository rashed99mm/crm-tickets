# ADR 0013 — Keep named error codes; `AC-66`'s numbering is not met

- **Status:** Accepted
- **Date:** 2026-08-26

## Context

`AC-66` fixes a numbered code per condition: `ERR011` duplicate email, `ERR012` delete guard,
`ERR021`/`ERR022` invalid and self transition, `ERR023` ownership, `ERR024` concurrency,
`ERR051`/`ERR052` payload size and media type.

The adopted platform emits **named** codes — `CUSTOMER_EMAIL_EXISTS`, `CUSTOMER_HAS_TICKETS`,
`TICKET_TRANSITION_NOT_ALLOWED`, `TICKET_ALREADY_IN_STATUS`, `TICKET_NOT_ASSIGNED_TO_YOU`,
`TICKET_MODIFIED_BY_ANOTHER_USER` — and has since ADR-0009. There are **131 such codes**, each a
constant in `ApplicationErrors` and a key in `Resources.yaml`, and the Angular client matches on
them.

The divergence was recorded in `FEAT-06`'s task 2 and in three story files, deferred each time to
this feature. `FEAT-09` is the pass that proves the contract across the whole surface, so it is
where deferring stops.

## Decision

**Keep the named codes. `AC-66` is not met, and that is recorded as a gap rather than argued away.**

The criterion's *intent* — one stable, documented code per condition, so a client can branch on it —
is satisfied: every condition has exactly one code, the codes are stable, and
`EveryErrorCode_HasABilingualMessage` proves all 131 resolve to a message. What is not satisfied is
the criterion's *literal text*, which names specific strings.

The spec should be amended to adopt the named vocabulary. Until it is, traceability shows `AC-66` as
a gap.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **Rename the eight codes `AC-66` names** | Cheap, and produces a mixed vocabulary: eight opaque `ERRnnn` beside 123 named ones. A reader would have to know which conditions happened to be enumerated in one criterion. Worse than either consistent choice. |
| **Renumber all 131 to `ERRnnn`** | Consistent, and satisfies the letter. It replaces self-describing codes with opaque ones across features this slice does not own, breaks the inherited tests and the Angular error handling, and costs a day for no behavioural gain. `ERR011` tells a maintainer nothing that `CUSTOMER_EMAIL_EXISTS` does not. |
| **Declare the two equivalent and mark `AC-66` done** | The tempting one, and dishonest. The criterion names strings; the system emits different strings. Recording that as met is exactly the "claim work complete on code that does not do it" failure `CLAUDE.md` forbids. |
| **Emit both — a `code` and a legacy `numericCode`** | Satisfies both texts and doubles the contract. Two ways to identify one condition is how clients end up branching on the wrong one. |

## Consequences

**Easier.** Codes stay self-describing, the inherited surface is untouched, and the client keeps
matching on names it can read.

**Harder.** An approved criterion is knowingly unmet, and the traceability table carries a gap that
a reviewer will ask about. That is the honest cost: the answer is this ADR.

**Reversible.** A rename is mechanical — constants, YAML keys, and the assertions naming them —
should the spec amendment go the other way.
