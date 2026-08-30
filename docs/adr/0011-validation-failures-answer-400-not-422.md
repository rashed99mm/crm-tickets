# ADR 0011 — Validation failures answer 400, not the inherited 422

- **Status:** Accepted
- **Date:** 2026-08-25

## Context

The approved slice specification is explicit and repeats itself across five criteria:

- **AC-8** — a missing name, a malformed email or an over-length field returns **400** with errors
  keyed by field name.
- **AC-11** — a `pageSize` above the server maximum returns **400**.
- **AC-30** — a missing subject, an over-length field or an invalid priority returns **400** keyed by
  field.
- **AC-31** — an unknown customer or category on ticket creation returns **400** identifying which.
- **AC-51** — validation failures carry top-level `VAL001` and one `errors[]` entry per field.

The adopted platform disagrees. `ResultActionResultExtensions.MapFailureStatusCode` maps
`ErrorType.Validation` to **422 Unprocessable Entity**, and two inherited integration tests assert
that — `ChangePassword_WrongCurrentPassword_Returns422KeyedToCurrentPassword` and
`ChangePassword_WeakNewPassword_Returns422KeyedToNewPassword`.

This had to be settled before `FEAT-03` and `FEAT-04` were written rather than after. The ticket
create form is the first screen that consumes a field-keyed rejection, and a form written against
one status code and a server answering the other is precisely the integration failure that shipping
vertically is meant to surface early.

The spec's taxonomy also does not treat 400 and 422 as interchangeable. **AC-38** turns on the
distinction: a refused status transition is 409 and *not* 400, "because the request is well-formed
and the state is wrong". That sentence only means something if a *malformed* request is 400. Reading
this spec's 400 as 422 would leave 400 unused and break the contrast the criterion is built on.

## Decision

`ErrorType.Validation` maps to **400 Bad Request**. The two inherited tests are updated to assert
400, and their comments are corrected to describe the mapping as it now stands.

The other mappings are untouched: `Conflict` → 409, `NotFound` → 404, `Forbidden` → 403,
`Unauthorized` → 401, everything else → 400.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **Keep 422 and amend the spec** | Defensible on the standards alone — RFC 4918's 422 is exactly "well-formed but semantically invalid", and a case can be made that it describes a validation failure more precisely than 400 does. It lost on three counts: the spec is approved and amending five criteria to match an inherited default inverts the SDD gate; AC-38's reasoning depends on 400 meaning malformed; and 422 is the less widely handled code in client tooling. |
| **Map 422 for body validation, 400 for query-string validation** | Would satisfy AC-11 and AC-30 separately by accident of where the value travelled. Two codes for one condition is the drift the shared composition core exists to prevent, and no criterion distinguishes them. |
| **Leave the mapping and translate 422 to 400 in the Angular interceptor** | Hides a server contract defect in a client. The API is the deliverable and is graded directly; a second consumer would get 422 with no interceptor to save it. |

## Consequences

**Easier.** One status code for one condition across both hosts, matching five approved criteria and
the contrast AC-38 relies on. The frontend binds `errors[]` to controls on a 400 and needs no special
case.

**Harder.** Two inherited tests change, so the suite is no longer byte-identical to the reference —
`BASE-2`'s "all inherited tests pass" now means *as amended here*, and that has to be said rather
than glossed. Any external consumer written against the reference platform's 422 sees a different
code; there is none today, and this is the cheapest moment there will ever be to make the change.

**Reversible.** One `switch` arm and two assertions.
