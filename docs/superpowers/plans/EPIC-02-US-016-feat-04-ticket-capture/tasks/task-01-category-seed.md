# Task 1 — Seed the fixed category list

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — task 1.1 |
| Feature | `FEAT-04` Ticket capture |
| Criteria | assumption `A4`, `BR-14` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Infrastructure/Seeders/CategorySeeder.cs`
- `src/CustomerSupport.Infrastructure/ServiceCollectionExtensions.cs` (registration)
- `src/CustomerSupport.Api.Shared/Extensions/WebApplicationExtensions.cs` (invocation)

## Test evidence

`Categories_AreSeededAndListedForThePicker` asserts all four names are present through the API. The
idempotence and race behaviour are exercised implicitly and hard: xUnit runs test classes in
parallel and each one starts a host, so the seeder runs several times concurrently on every suite
run. Suite: **193 passed, 0 failed.**

## Deviations from the plan

**1. The seeder crashed every parallel host start, and had to be made race-tolerant.**
Read-then-insert is not atomic. Two hosts starting at once both see the categories missing, both
insert, and the loser hits `UX_Categories_Name` — which crashed start-up with
`Invalid object name` / duplicate-key failures across the whole integration suite.

The recovery is deliberately not a bare `catch`. It detaches the failed inserts so the context stays
usable, then **re-reads and confirms the rows actually exist**, rethrowing if they do not. Swallowing
every `DbUpdateException` would have hidden genuine faults behind a race that may not have happened.

**2. This was not a test-only problem.**
It looked like one, and treating it as one would have been the wrong fix. A rolling deploy starts two
hosts at once and would have hit exactly the same crash in production. The parallel test run
surfaced it early, which is the argument for not serialising the suite to make it go away.

**3. `IgnoreQueryFilters()` on the existence read.**
`Category` is soft-deletable, so a deactivated-and-deleted category would be invisible to the default
filter and the seeder would try to re-insert its name — straight into the filtered unique index,
which still holds it only if the row is live. Reading past the filter makes the check match what the
index actually enforces.

## The point of this task

Categories are a **closed vocabulary**, and `BR-14` refuses free text for a reason that only shows up
later: reporting has to group by something stable. Four buckets an agent can choose between without
thinking beats twenty that invite miscategorisation, so the list is deliberately short.

Seeding is administrative, so it runs on the internal host only — `BASE-7` keeps the external host
out of it.
