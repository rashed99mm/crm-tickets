# Task 5 — Expose the category list · **not in the plan**

| Field | Value |
|---|---|
| Plan | [`implementation-plan/implementation-plan.md`](../implementation-plan.md) — **no corresponding task** |
| Feature | `FEAT-04` Ticket capture |
| Criteria | none directly; required by `US-127` / `AC-59` |
| Status | `done` |
| Commit | uncommitted — working tree |

## Files

- `src/CustomerSupport.Application/Features/Tickets/Queries/GetCategories/GetCategoriesQuery.cs`
- `src/CustomerSupport.InternalApi/Controllers/CategoriesController.cs`
- `tests/CustomerSupport.Tests/Integration/TicketEndpointTests.cs`

## Test evidence

`Categories_AreSeededAndListedForThePicker` — 200, and all four seeded names present.
Suite: **193 passed, 0 failed.**

## Why this task exists at all

**The backend plan had a hole and this is it.** Task 1.1 seeded the categories and nothing exposed
them. The gap was invisible while the backend was the only thing being built, and surfaced the
moment the create form needed a picker: the frontend had a required `categoryId` field and no way to
discover a single valid value.

Without the endpoint the form would have had to accept free text, which `BR-14` refuses outright.

This is a **plan defect, not a scope change**. `US-127` always required a category picker; the
backend plan simply failed to derive the endpoint from the frontend story it names as its
counterpart. Caught within the same day only because the feature loop puts the frontend immediately
after the backend — under the layered plan this project replaced, it would have surfaced sprints
later.

## Deviations from the plan

Not applicable — there was no plan for this task. Design decisions taken while writing it:

1. **Unpaged.** A closed, seeded list of four. Paging it would make the picker do two round trips to
   render a dropdown. If categories ever become user-managed, this changes.
2. **Read-only.** No create, update or delete. The list is a developer concern until a later slice
   says otherwise (`A4`), and endpoints nothing asked for are the easiest place for scope to leak.
3. **`Authenticated`, not a role policy.** Consistent with the rest of the Day 1 surface; no
   criterion restricts who may read the category list.
4. **Active only.** Matches the check `CreateTicketCommandHandler` performs, so the picker cannot
   offer a value the create would then reject.
