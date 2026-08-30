# ADR 0003 — Use ASP.NET Core Identity for authentication

- **Status:** Accepted
- **Date:** 2026-08-24

## Context

S1 needs authentication and two roles (`Agent`, `Supervisor`) to support AC-1 through AC-6 and the
per-record authorization rules in AC-42 through AC-47.

The constraint that shapes this is time: two to three working days for a scope that also includes
customer CRUD, notes, attachments, the ticket status machine, a frontend and a test suite.

## Decision

Use ASP.NET Core Identity with EF Core stores, issuing JWTs for the Angular client. Two seeded
roles, seeded users, no public self-registration (assumption A1).

**This was chosen against the recommendation on this record.** The advice was a minimal
hand-rolled JWT endpoint using `PasswordHasher<T>`, on the grounds that it costs roughly half a
day rather than most of one, is entirely explainable line by line, and leaves more time for the
status machine and tests — which carry more acceptance criteria and score against more rubric
criteria. The decision to use Identity was taken with that trade-off stated.

## Alternatives considered

| Option | Why it lost |
|---|---|
| **Minimal JWT + `PasswordHasher<T>` + seeded users** | The recommended option. Faster, fully testable, and every line is yours to explain. It lost on the explicit preference for Identity — a defensible one: Identity is what a real team would reach for, and hand-rolling auth is a pattern worth being cautious about even when done correctly. |
| **Fake auth via a header naming the user** | Forfeits the security criterion almost entirely. Per-record authorization tests would still work, but nothing would demonstrate authentication. Rejected outright. |
| **External identity provider (Entra ID, Keycloak)** | Correct for production, wrong here: setup and tenant configuration would consume most of the available time and the assessment cannot exercise it offline. |

## Consequences

- Lockout, password hashing, and normalised-email uniqueness come for free and correctly, which
  is a real security gain over hand-rolled code — AC-6 in particular is nearly free.
- Identity's schema arrives in the database: several tables this slice does not use. Harmless, but
  worth being able to explain rather than being surprised by.
- **The main cost is time,** on the tightest constraint in the project. The spec's build order
  puts Identity first precisely because it is the step most likely to overrun, and the cut lines
  protect the test suite from absorbing that overrun.
- Framework behaviour has to be understood, not just wired. Being unable to explain how token
  validation or lockout works would cost more on the ownership criterion than hand-rolled code
  would have.
- Identity's `AppUser` lives in `Infrastructure`, not `Domain` — it is a persistence and framework
  concern. `Domain` refers to actors by id only, which keeps ADR 0002's dependency rule intact.
