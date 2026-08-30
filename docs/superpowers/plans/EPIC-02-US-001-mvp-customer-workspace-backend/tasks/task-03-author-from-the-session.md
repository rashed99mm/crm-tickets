# Task 3 — The author comes from the session

| Field | Value |
|---|---|
| Plan | [`implementation-plan.md`](../implementation-plan.md) — T1, T2 |
| Feature | `MVP-05` Interaction history (backend half) |
| Criteria | **`AC-76`** |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Customers/Dtos/CustomerNoteDtos.cs`
- `src/CustomerSupport.Application/Features/Customers/Commands/AddCustomerNote/AddCustomerNoteCommand.cs`
- `tests/CustomerSupport.Tests/Integration/CustomerNotesEndpointTests.cs`

## Test evidence

- `AC76_AddNote_AuthorComesFromTheTokenNotThePayload` — posts
  `{ body, authorId: <another real user>, createdBy: <another real user> }` and asserts the stored
  note's `authorId` **is** the caller and **is not** the value sent.

Against real LocalDB. Suite: **250 passed, 0 failed.**

## Deviations from the plan

None. The plan's sketch was followed, with one addition: the payload carries `createdBy` as well as
`authorId`, because `BaseEntity` has a `CreatedBy` column and a second plausible name for the same
attack costs one line to close off.

## The point of this task

There are three layers between a caller and a forged author, and each one is independently
sufficient:

1. `CreateCustomerNoteRequest` has **no author field**, so an `authorId` in the JSON binds to
   nothing and is discarded during model binding.
2. `AddCustomerNoteCommand` has no author property either, so nothing could be threaded through
   even if the request record grew one by accident.
3. `CustomerNote.Create` takes `authorId` as a required argument with no default, and the handler
   passes `userContext.UserId` — the only value in scope.

The **test** is what makes this a criterion rather than a claim. A test that merely omitted
`authorId` would pass just as happily against a handler that honoured it, which is why the plan
insists on posting the field and asserting it was ignored. If the author were forgeable, the
interaction history would be worthless as a record — the whole reason `MVP-05` exists.
