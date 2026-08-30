# Task 1 — Add a note

| Field | Value |
|---|---|
| Plan | [`implementation-plan.md`](../implementation-plan.md) — T1, T2, T4, T5, T6 |
| Feature | `MVP-05` Interaction history (backend half) |
| Criteria | `AC-75`, and the inherited `AC-20` path rule |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Customers/Dtos/CustomerNoteDtos.cs` (new)
- `src/CustomerSupport.Application/Features/Customers/Commands/AddCustomerNote/AddCustomerNoteCommand.cs` (new)
- `src/CustomerSupport.Application/Features/Customers/Validators/CustomerValidators.cs`
- `src/CustomerSupport.Application/Errors/ApplicationErrors.cs`
- `src/CustomerSupport.Api.Shared/Localization/Resources.yaml`
- `src/CustomerSupport.InternalApi/Controllers/CustomersController.cs`
- `tests/CustomerSupport.Tests/Integration/CustomerNotesEndpointTests.cs` (new)

## Test evidence

- `AC75_AddNote_ValidBody_Returns201AndAppearsInTheList` — 201, `Location` header present, and the
  note is readable afterwards. The read-back is the assertion that matters: a route answering 201
  and storing nothing would satisfy the status check alone.
- `AC75_AddNote_EmptyBody_Returns400KeyedToBody` — whitespace-only, `VALIDATION_ERROR`, `details`
  keyed to `Body`, and nothing written.
- `AC20_AddNote_UnknownCustomer_Returns404` — code `CUSTOMER_NOT_FOUND`.
- `AC20_GetNotes_UnknownCustomer_Returns404`.
- `AC3_Notes_WithoutAToken_Returns401`.

Against real LocalDB, not the in-memory provider. Suite: **250 passed, 0 failed.**

## Deviations from the plan

**`Location` points at the list, not at the note.** The plan says `201 + Location -> GetNotes`, and
that is what shipped, but it is worth saying why rather than leaving it looking like an oversight:
there is no single-note route to point at, because `AC-74` reads the history as a whole and
`A13`/`A11` mean a note is never fetched, edited or deleted on its own.

**`NOTE_BODY_REQUIRED` rather than reusing `BODY_REQUIRED`.** The existing `BODY_REQUIRED` belongs
to the knowledge-base content surface. Sharing it would tie two unrelated features' wording
together for the sake of one saved constant.

## The point of this task

`RuleFor(x => x.Body)` is written against the property directly. FluentValidation derives
`ValidationFailure.PropertyName` from the member expression, so a rule built over an invoked `Func`
arrives with an empty field key — a 400 the note box cannot bind to, which is most of `AC-75`. That
mistake was made once in `FEAT-03` and is recorded there; it was not repeated here.

The field key reaches the wire as `Body`, PascalCase, because that is the member name
FluentValidation reports. The Angular interceptor lowercases it. That is the contract, not a defect.
