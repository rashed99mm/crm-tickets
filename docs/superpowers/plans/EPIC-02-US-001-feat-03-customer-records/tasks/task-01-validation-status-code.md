# Task 0 — Validation failures answer 400, not the inherited 422

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — Task 0 |
| Feature | `FEAT-03` Customer records (blocks every task after it) |
| Criteria | `AC-8`, `AC-11`, `AC-30`, `AC-31`, `AC-51` |
| Status | `done` |
| Commit | uncommitted — working tree |
| Decision | [ADR-0011](../../../../adr/0011-validation-failures-answer-400-not-422.md) |

## Files

- `src/CustomerSupport.Api.Shared/Extensions/ResultActionResultExtensions.cs`
- `tests/CustomerSupport.Tests/Integration/ChangePasswordEndpointTests.cs`
- `docs/adr/0011-validation-failures-answer-400-not-422.md`

## Test evidence

The two inherited tests are the test. Renamed from `…Returns422KeyedTo…` to `…Returns400KeyedTo…`
and their assertions changed to `HttpStatusCode.BadRequest`:

- `ChangePassword_WrongCurrentPassword_Returns400KeyedToCurrentPassword`
- `ChangePassword_WeakNewPassword_Returns400KeyedToNewPassword`

Full suite after the change: **193 passed, 0 failed.**

## Deviations from the plan

None. This ran first exactly as planned, and doing so was the point — every task below asserts 400
on a validation failure, and discovering at task 5 that the platform answered 422 would have meant
rewriting five sets of assertions.

## The point of this task

The spec names 400 in five separate criteria, and the platform answered 422 in one place. Either was
defensible in isolation; what is not defensible is a form written against one and a server answering
the other.

`AC-38` is what settled it. It requires a refused status transition to be **409 and not 400**,
"because the request is well-formed and the state is wrong". That sentence only carries meaning if a
*malformed* request is 400. Reading this spec's 400 as 422 would have left 400 unused and destroyed
the contrast the criterion is built on.

The cost is stated rather than hidden: `BASE-2`'s "all 97 inherited tests pass" now means *as
amended here*, and that is written into the ADR's consequences.
