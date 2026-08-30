---
name: dotnet-testing
description: Use when writing or reviewing any backend test in this project - decides which test level a case belongs at, covers xUnit layout, WebApplicationFactory integration tests, database testing, and what must never be mocked
---

# Backend testing

## Overview

Three test projects mirroring the source layout:

```
tests/Domain.Tests/           pure unit, zero infrastructure, milliseconds
tests/Application.Tests/      handlers with ports faked
tests/Api.IntegrationTests/   real HTTP, real database
```

**Choosing the level is the whole skill.** Everything drifts upward — a case that could be a
fast unit test gets written as an integration test because the setup was already there — and a
suite that takes four minutes stops being run.

| Question | Level |
|---|---|
| Is an invariant enforced? | Domain unit |
| Does the use case orchestrate correctly (loads, mutates, saves, publishes)? | Application, ports faked |
| Does the SQL / mapping / constraint work? | Integration |
| Does the endpoint return the right status, shape and headers? | Integration |
| Does the whole feature work for a user? | E2E (frontend project) |

## Naming

Name tests after the acceptance criterion they prove, so "show me where AC-4 is tested" is
answerable in seconds:

```csharp
[Fact] // AC-4
public async Task Create_Rejects_Title_Over_200_Chars() { ... }
```

`Method_Scenario_ExpectedOutcome`. A test named `Test1` or `CreateEventTest` documents nothing.

## Application tests

Fake the ports, exercise the handler. Prefer hand-written fakes (an in-memory list behind
`IEventRepository`) over mock frameworks for repositories — a fake that actually stores things
catches ordering and idempotency bugs that `Verify()` assertions miss.

**Do not assert on mock calls when you can assert on outcomes.** A `Verify(x => x.Save(...))`
passes when `Save` was called with the wrong data that got reshaped later. Assert the state that
resulted.

Freeze time behind an `IClock` port. Tests using `DateTime.UtcNow` fail at midnight, at month
boundaries, and in another timezone — always on someone else's machine.

## Integration tests

`WebApplicationFactory<Program>` for real HTTP through the real pipeline. This is what proves
routing, model binding, auth, validation and serialisation actually compose — none of which a
handler unit test touches.

Database options, in order of preference:

1. **Testcontainers** with the real engine. Highest fidelity, needs Docker running. Docker
   27.3.1 is present on this machine, but `docker run` has been unreliable here — verify it
   works before committing the suite to it.
2. **A real local instance** with a per-run database name.
3. **SQLite in-memory** — fast, but a different provider. It will not catch schema, collation or
   provider-specific translation problems, and it accepts some queries the real database rejects.

**Never `UseInMemoryDatabase` for anything meaningful.** It is not a relational database: it
ignores constraints, unique indexes, foreign keys and transactions, so it passes tests that the
real database fails. That is worse than having no test, because it produces confidence you have
not earned.

Each test starts from a known state — a transaction rolled back per test, or a reset between
tests. Tests that depend on execution order fail unpredictably in CI and cost hours to diagnose.

## What to test

Per acceptance criterion, cover:

- The happy path.
- **Every negative path** — invalid input, missing record, conflicting state, forbidden user,
  unauthenticated caller. This is where the "Testing, Security & Edge Cases" marks live, and
  where thin suites are always thin.
- Boundaries: empty collection, exactly-at-limit, one over, zero, negative, maximum length,
  unicode, null where nullable.
- Concurrency, wherever two callers can race the same record.

Assert the response *shape*, not only the status. A 400 that returns an empty error body still
breaks the frontend that reads per-field errors.

## Running them

Run the suite before claiming anything about it, and **paste the actual output**. "Tests should
pass" is not a result. A failing test reported as failing is fine and useful; a failing test
reported as passing is the worst outcome available here.

## Red flags

| Thought | Reality |
|---|---|
| "InMemoryDatabase is close enough" | It ignores constraints and unique indexes. It passes tests the real DB fails. |
| "I'll test it through the API since the setup exists" | Then it runs in seconds instead of milliseconds, and the suite stops being run. |
| "Mock verification proves it saved" | It proves a call happened. Assert the resulting state. |
| "Happy path covers it" | Half the testing criterion is failure scenarios. |
| "It passes locally" | Order dependence and `DateTime.UtcNow` both pass locally. Check isolation. |
| "The build succeeded, so it works" | A clean build is not a passing test. Run them. |
